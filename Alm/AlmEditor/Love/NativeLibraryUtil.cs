﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Diagnostics;

namespace Love
{
    public static class NativeLibraryUtil
    {

        public interface INativeLibraryLoader
        {
            bool LoadLibrary(string path, out IntPtr libPtr, out string errorInfo);
            bool LoadFunc(IntPtr libPtr, string functionName, out IntPtr funcPtr, out string errorInfo);
            Arch GetArch();
        }

        public enum Arch
        {
            X86_32,
            X86_64,
            Unknow,
        }

        class WindowsNativeLibraryLoader : INativeLibraryLoader
        {
            #region Process Info
            
            public enum Architecture
            {
                x86,
                /// <summary>
                /// x64 (AMD or Intel)
                /// </summary>
                x64,
                /// <summary>
                ///
                /// </summary>
                ARM,
                /// <summary>
                ///
                /// </summary>
                ARM64,

                Unknow,
            }
            #endregion

            public Arch GetArch()
            {
                if (IntPtr.Size == 4)
                    return Arch.X86_32;

                if (IntPtr.Size == 8)
                    return Arch.X86_64;

                return Arch.Unknow;
            }

            [DllImport("kernel32")]
            public static extern IntPtr LoadLibrary(string fileName);
            [DllImport("kernel32")]
            public static extern IntPtr GetProcAddress(IntPtr module, string procName);
            [DllImport("kernel32")]
            public static extern int FreeLibrary(IntPtr module);

            public bool LoadLibrary(string path, out IntPtr libPtr, out string errorInfo)
            {
                libPtr = LoadLibrary(path);
                if (libPtr == IntPtr.Zero)
                {
                    errorInfo = ($"load lib error: {path} {Marshal.GetLastWin32Error()}");
                    return false;
                }

                errorInfo = "";
                return true;
            }

            public bool LoadFunc(IntPtr libPtr, string functionName, out IntPtr funcPtr, out string errorInfo)
            {
                funcPtr = GetProcAddress(libPtr, functionName);
                if (funcPtr == IntPtr.Zero)
                {
                    errorInfo = ($"load func error: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                errorInfo = "";
                return true;
            }
        }

        class UnixNativeLibraryLoader : INativeLibraryLoader
        {
            // public const int RTLD_LAZY = 0x001;
            public const int RTLD_NOW = 0x002;
            [DllImport("libdl")]
            public static extern IntPtr dlopen(string fileName, int flags);
            [DllImport("libdl")]
            public static extern string dlerror();
            [DllImport("libdl")]
            public static extern IntPtr dlsym(IntPtr handle, string name);

            public Arch GetArch()
            {
                // FIXME:
                // need use RuntimeInformation.ProcessArchitecture in at least .NET Framework 4.7.1 or .NET Core 1.0
                if (IntPtr.Size == 4)
                    return Arch.X86_32;

                if (IntPtr.Size == 8)
                    return Arch.X86_64;

                return Arch.Unknow;
            }

            public bool LoadLibrary(string path, out IntPtr libPtr, out string errorInfo)
            {
                libPtr = dlopen(path, RTLD_NOW);
                if (libPtr == IntPtr.Zero)
                {
                    errorInfo = ($"load lib error: {path} {dlerror()}");
                    return false;
                }

                errorInfo = "";
                return true;
            }

            public bool LoadFunc(IntPtr libPtr, string functionName, out IntPtr funcPtr, out string errorInfo)
            {
                funcPtr = dlsym(libPtr, functionName);
                if (funcPtr == IntPtr.Zero)
                {
                    errorInfo = ($"load func error: {dlerror()}");
                    return false;
                }

                errorInfo = "";
                return true;
            }
        }

        public delegate bool FunctionAddrLoaderDelegate(string functionName, out IntPtr funcPtr, out string errorInfo);

        public class FunctionAddrLoader
        {
            readonly INativeLibraryLoader loader;
            readonly Dictionary<string, IntPtr> libAddrDict = new Dictionary<string, IntPtr>();

            internal FunctionAddrLoader(INativeLibraryLoader loader, Dictionary<string, IntPtr> libAddrDict)
            {
                this.loader = loader;
                this.libAddrDict = new Dictionary<string, IntPtr>(libAddrDict);
            }

            public FunctionAddrLoaderDelegate GetFunctionLoader(string libPath)
            {
                var libAddr = libAddrDict[libPath];
                return (string functionName, out IntPtr funcPtr, out string errorInfo) => loader.LoadFunc(libAddr, functionName, out funcPtr, out errorInfo);
            }
        }

        static class NativlibTool
        {
            public static void WriteToTempDir(string tempDir, LibraryContent[] libToLoad)
            {
                foreach (var lib in libToLoad)
                {
                    try
                    {
                        var content = lib.LibraryContentGenerator() ?? throw new Exception(" LibraryContentGenerator return null !");
                        WriteContentToDir(content, Path.Combine(tempDir, lib.FullPath));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("error on load " + Path.Combine(tempDir, lib.FullPath) + " " + ex);
                    }
                }
            }



            public static byte[] ReadFully(System.IO.Stream input)
            {
                byte[] buffer = new byte[16 * 1024];
                using (input)
                {
                    if (input == null)
                    {
                        throw new ArgumentNullException(nameof(input));
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        return ms.ToArray();
                    }
                }
            }

            public static void FolderCheck(string fullPath)
            {
                var fi = new System.IO.FileInfo(fullPath);
                fi.Directory.Create();
            }

            public static void WriteContentToDir(System.IO.Stream stream, string targetFilePath)
                => WriteContentToDir(ReadFully(stream), targetFilePath);

            public static void WriteContentToDir(byte[] bytes, string targetFilePath)
            {
                FolderCheck(targetFilePath);
                var dllPath = new System.IO.FileInfo(targetFilePath);
                var rewrite = true;
                if (dllPath.Exists)
                {
                    var existing = System.IO.File.ReadAllBytes(dllPath.FullName);
                    if (bytes.SequenceEqual(existing))
                    {
                        rewrite = false;
                    }
                }

                if (rewrite)
                {
                    System.IO.File.WriteAllBytes(dllPath.FullName, bytes);
                }
            }

            public static string GetHashAssembly()
            {
                var assem = Assembly.GetEntryAssembly();
                return assem.GetName().Name + "." + GetHashString(assem.Location);
            }

            public static byte[] GetHash(string inputString)
            {
                var algorithm = System.Security.Cryptography.MD5.Create();
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
            }

            public static string GetHashString(string inputString)
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in GetHash(inputString))
                    sb.Append(b.ToString("X2"));

                return sb.ToString();
            }
        }

        public enum LibraryLoaderPlatform
        {
            Win32,
            Win64,
            Linux32,
            Linux64,
            Mac64,
            Unknow,
        }

        /// <summary>
        /// https://github.com/Pkcs11Interop/Pkcs11Interop/blob/3.2.0/src/Pkcs11Interop/Pkcs11Interop/Common/Platform.cs#L228-L254
        /// </summary>
        static public class PlatformDetecter
        {

            /// <summary>
            /// True if runtime platform is Windows
            /// </summary>
            private static bool _isWindows = false;

            /// <summary>
            /// True if runtime platform is Windows
            /// </summary>
            public static bool IsWindows
            {
                get
                {
                    DetectPlatform();
                    return _isWindows;
                }
            }

            /// <summary>
            /// True if runtime platform is Linux
            /// </summary>
            private static bool _isLinux = false;

            /// <summary>
            /// True if runtime platform is Linux
            /// </summary>
            public static bool IsLinux
            {
                get
                {
                    DetectPlatform();
                    return _isLinux;
                }
            }

            /// <summary>
            /// True if runtime platform is Mac OS X
            /// </summary>
            private static bool _isMacOsX = false;

            /// <summary>
            /// True if runtime platform is Mac OS X
            /// </summary>
            public static bool IsMacOsX
            {
                get
                {
                    DetectPlatform();
                    return _isMacOsX;
                }
            }

            static void DetectPlatform()
            {
                // Supported platform has already been detected
                if (_isWindows || _isLinux || _isMacOsX)
                    return;

                // Detect platform
                //
                // System.Environment.OSVersion.Platform is not used because:
                // - Mac OS X detection almost never works under Mono
                // - it is not implemented by .NET Core
                //
                // System.Runtime.InteropServices.RuntimeInformation is not used because:
                // - it is not implemented by full .NET and Mono
                // - it does not perform platform detection in runtime but uses hardcoded information instead
                //   See https://github.com/dotnet/corefx/issues/3032 for more info
                //
                // Pinvoking of platform specific unmanaged functions is not used because:
                // - it may cause segmentation fault on unknown platforms
                //
                // Following code may look silly but:
                // - it is 100% managed code
                // - it works under .NET, Mono and .NET Core
                // - it works like a charm so far

                string windir = Environment.GetEnvironmentVariable("windir");
                if (!string.IsNullOrEmpty(windir) && windir.Contains(@"\") && Directory.Exists(windir))
                {
                    _isWindows = true;
                }
                else if (System.IO.File.Exists(@"/proc/sys/kernel/ostype"))
                {
                    string osType = System.IO.File.ReadAllText(@"/proc/sys/kernel/ostype");
                    if (osType.StartsWith("Linux", StringComparison.OrdinalIgnoreCase))
                    {
                        // Note: Android gets here too
                        _isLinux = true;
                    }
                    else
                    {
                        throw new Exception(string.Format("LoveShparp is not supported on \"{0}\" platform", osType));
                    }
                }
                else if (System.IO.File.Exists(@"/System/Library/CoreServices/SystemVersion.plist"))
                {
                    // Note: iOS gets here too
                    _isMacOsX = true;
                }
                else
                {
                    throw new Exception("LoveShparp is not supported on this platform");
                }
            }
        }

        static public partial class LibraryLoader
        {
            public static string TempFolderPrefix =>
                 new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"{NativlibTool.GetHashAssembly()}"))
                .FullName;

            static FunctionAddrLoader GetFunctionAddrLoader(LibraryContent[] libToLoad, INativeLibraryLoader loader)
            {
                var assem = Assembly.GetExecutingAssembly();
                var an = assem.GetName();
                var asseName = an.Name + ".";
                var tempDirName = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"{NativlibTool.GetHashAssembly()}.{loader.GetArch()}.{an.Version}"));
                // 1. create temp folder
                {
                    if (!tempDirName.Exists)
                    {
                        tempDirName.Create();
                    }
                }

                // 2. copy to file
                {
                    NativlibTool.WriteToTempDir(tempDirName.FullName, libToLoad);
                }

                // 3. load library
                Dictionary<string, IntPtr> libAddrDict = new Dictionary<string, IntPtr>();
                {
                    foreach (var lib in libToLoad)
                    {
                        var fileToLoadPath = Path.Combine(tempDirName.FullName, lib.FullPath);
                        if (loader.LoadLibrary(fileToLoadPath, out var libAddrPtr, out var errorInfo) == false)
                        {
                            throw new Exception($"load lib error: {fileToLoadPath} {errorInfo}");
                        }

                        libAddrDict.Add(lib.FullPath, libAddrPtr);
                    }
                }

                return new FunctionAddrLoader(loader, libAddrDict);
            }

            public static LibraryLoaderPlatform GetPlatform(out INativeLibraryLoader loader)
            {
                // 判断操作系统平台
                OperatingSystem os = Environment.OSVersion;
                PlatformID pid = os.Platform;
                switch (pid)
                {
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                        loader = new WindowsNativeLibraryLoader();
                        return loader.GetArch() == Arch.X86_32 ? LibraryLoaderPlatform.Win32 : LibraryLoaderPlatform.Win64;
                    case PlatformID.Unix:
                        loader = new UnixNativeLibraryLoader();
                        bool is64 = loader.GetArch() == Arch.X86_64;
                        if (PlatformDetecter.IsMacOsX)
                        {
                            return LibraryLoaderPlatform.Mac64;
                        }
                        else
                        {
                            return is64 ? LibraryLoaderPlatform.Linux64 : LibraryLoaderPlatform.Linux32;
                        }
                    default:
                        loader = null;
                        return LibraryLoaderPlatform.Unknow;
                }
            }

            public static FunctionAddrLoader Load(LibraryConfig config)
            {
                LibraryContent[] libToLoad = null;
                var pt = GetPlatform(out var loader);
                switch (pt)
                {
                    case LibraryLoaderPlatform.Win32: libToLoad = config.Win32; break;
                    case LibraryLoaderPlatform.Win64: libToLoad = config.Win64; break;
                    case LibraryLoaderPlatform.Linux32: libToLoad = config.Linux32; break;
                    case LibraryLoaderPlatform.Linux64: libToLoad = config.Linux64; break;
                    case LibraryLoaderPlatform.Mac64: libToLoad = config.Mac64; break;

                    default:
                        throw new Exception("unknow platform");
                }

                return GetFunctionAddrLoader(libToLoad ?? throw new Exception("un set the platform native on " + pt), loader);
            }

        }

        public class LibraryContent
        {
            public string FullPath;
            public Func<byte[]> LibraryContentGenerator;

            public LibraryContent(string fullPath, Func<byte[]> libraryContentGenerator)
            {
                FullPath = fullPath ?? throw new ArgumentNullException(nameof(fullPath));
                LibraryContentGenerator = libraryContentGenerator ?? throw new ArgumentNullException(nameof(libraryContentGenerator));
            }
        }

        public class LibraryConfig
        {
            public LibraryContent[] Linux32 { get; set; }
            public LibraryContent[] Linux64 { get; set; }
            public LibraryContent[] Mac64 { get; set; }
            public LibraryContent[] Win32 { get; set; }
            public LibraryContent[] Win64 { get; set; }
        }

    }
}
