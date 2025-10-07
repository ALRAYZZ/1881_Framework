fx_version 'bodacious'
game 'gta5'

-- Register all DLLs (including Newtonsoft.Json.dll) so they can be found
file 'Client/bin/Release/**/publish/*.dll'

ui_page 'Client/bin/Release/net452/publish/NUI/index.html'

files {
    'Client/bin/Release/net452/publish/NUI/index.html',
    'Client/bin/Release/net452/publish/NUI/app.js',
    'Client/bin/Release/net452/publish/NUI/style.css',
}

author 'ALRAYZZ'
version '1.0.0'

client_script {
    'Client/bin/Release/net452/publish/UI.Client.net.dll',
    'Client/bin/Release/net452/publish/Newtonsoft.Json.dll'
}
server_script 'Server/bin/Release/**/publish/*.net.dll'