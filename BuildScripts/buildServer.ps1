$ErrorView = "NormalView"
echo $env:GITHUB_ACTION_PATH
cd "C:\Program Files\Unity\2020.3.19f1\Editor"
.\Unity.exe -batchmode -quit -nographics -projectPath .. -executeMethod BuildRunner.BuildServer