net stop IAS
copy /Y C:\dd\NET.NpsWrapper\x64\Debug\*.* c:\windows\system32
net start IAS
pause
sc query IAS
