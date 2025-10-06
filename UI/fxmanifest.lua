fx_version 'bodacious'
game 'gta5'



ui_page 'Client/bin/Release/net452/publish/NUI/index.html'

files {
    'Client/bin/Release/net452/publish/NUI/index.html',
    'Client/bin/Release/net452/publish/NUI/app.js',
    'Client/bin/Release/net452/publish/NUI/style.css',
}

author 'ALRAYZZ'
version '1.0.0'


client_script 'Client/bin/Release/**/publish/*.net.dll'
server_script 'Server/bin/Release/**/publish/*.net.dll'