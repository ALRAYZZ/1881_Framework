fx_version 'bodacious'
game 'gta5'

file 'Client/bin/Release/**/publish/*.dll'

files { 'Server/bin/Release/netstandard2.0/publish/Data/peds.json' }

client_script { 'Client/bin/Release/**/publish/*.net.dll',
                'Server/bin/Release/netstandard2.0/publish/Newtonsoft.Json.dll' }

server_script 'Server/bin/Release/**/publish/*.net.dll'

author 'ALRAYZZ'
version '1.0.0'
description 'PedManager C# helper for FiveM'

dependency 'Database'
dependency 'spawnmanager'