fx_version 'cerulean'
game 'gta5'

author 'ALRAYZZ'
description 'Database helper for C# scripts'
version '1.0.0'

server_script 'Server/bin/Release/**/publish/*.net.dll'

dependency 'oxmysql'

server_export 'Query'
server_export 'Scalar'
server_export 'Insert'