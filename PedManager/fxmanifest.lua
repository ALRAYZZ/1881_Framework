fx_version 'bodacious'
game 'gta5'

file 'Client/bin/Release/**/publish/*.dll'

client_script 'Client/bin/Release/**/publish/*.net.dll'
server_script 'Server/bin/Release/**/publish/*.net.dll'

author 'ALRAYZZ'
version '1.0.0'
description 'PedManager C# helper for FiveM'

dependency 'Database'
dependency 'spawnmanager'