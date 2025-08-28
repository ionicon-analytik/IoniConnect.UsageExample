set ZIPPER=C:\Program Files\7-Zip\7z.exe
set ARCHIVE=Z:\Ionicon\Projekte\AME2\RELEASE\UsageExample\IoniConnect.UsageExample.zip

"%zipper%" a -tzip "%archive%" -- Program.cs Dependencies\*.nupkg *.sln
