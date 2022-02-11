cd Dependices/megasource
git submodule init
git submodule update
git clone https://github.com/love2d/love libs/love
cmake -G "Visual Studio 16 2019" -A Win32 -H. -Bbuild
cmake --build build --target love/love --config Release
xcopy .\Dependices\megasource\build\love\Release .\Release\Debug\Libs /E