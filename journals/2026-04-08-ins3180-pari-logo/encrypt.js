const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const baseDir = path.dirname(process.argv[1] || __filename);
const html = fs.readFileSync(path.join(baseDir, 'report.html'), 'utf8');
const password = '2217';

const salt = crypto.randomBytes(16);
const iv = crypto.randomBytes(12);
const key = crypto.pbkdf2Sync(password, salt, 100000, 32, 'sha256');
const cipher = crypto.createCipheriv('aes-256-gcm', key, iv);
const encrypted = Buffer.concat([cipher.update(html, 'utf8'), cipher.final()]);
const tag = cipher.getAuthTag();
const ct = Buffer.concat([encrypted, tag]);

const lockPage = `<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>INS-3180</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#f0f2f5;display:flex;align-items:center;justify-content:center;min-height:100vh}
.lock{background:white;border-radius:16px;padding:40px;box-shadow:0 4px 24px rgba(0,0,0,0.1);text-align:center;max-width:360px;width:90%}
.lock svg{width:48px;height:48px;color:#6366f1;margin-bottom:16px}
.lock h2{font-size:20px;margin-bottom:8px;color:#1a1a1a}
.lock p{font-size:14px;color:#666;margin-bottom:20px}
.lock input{width:100%;padding:12px 16px;border:2px solid #e5e7eb;border-radius:10px;font-size:16px;outline:none;transition:border 0.2s}
.lock input:focus{border-color:#6366f1}
.lock button{width:100%;padding:12px;background:#6366f1;color:white;border:none;border-radius:10px;font-size:16px;cursor:pointer;margin-top:12px;transition:background 0.2s}
.lock button:hover{background:#4f46e5}
.error{color:#dc2626;font-size:13px;margin-top:8px;display:none}
</style>
</head>
<body>
<div class="lock">
<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0 2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25 2.25v6.75a2.25 2.25 0 0 0 2.25 2.25Z"/></svg>
<h2>Доступ по паролю</h2>
<p>Введите пароль для просмотра отчёта</p>
<input type="password" id="pw" placeholder="Пароль" autofocus>
<button onclick="decrypt()">Открыть</button>
<div class="error" id="err">Неверный пароль</div>
</div>
<script>
const SALT='${salt.toString('base64')}';
const IV='${iv.toString('base64')}';
const CT='${ct.toString('base64')}';
document.getElementById('pw').addEventListener('keydown',e=>{if(e.key==='Enter')decrypt()});
async function decrypt(){
  try{
    const pw=document.getElementById('pw').value;
    const enc=new TextEncoder();
    const keyMaterial=await crypto.subtle.importKey('raw',enc.encode(pw),'PBKDF2',false,['deriveKey']);
    const key=await crypto.subtle.deriveKey({name:'PBKDF2',salt:Uint8Array.from(atob(SALT),c=>c.charCodeAt(0)),iterations:100000,hash:'SHA-256'},keyMaterial,{name:'AES-GCM',length:256},false,['decrypt']);
    const ctBytes=Uint8Array.from(atob(CT),c=>c.charCodeAt(0));
    const decrypted=await crypto.subtle.decrypt({name:'AES-GCM',iv:Uint8Array.from(atob(IV),c=>c.charCodeAt(0))},key,ctBytes);
    const html=new TextDecoder().decode(decrypted);
    document.open();document.write(html);document.close();
  }catch(e){
    document.getElementById('err').style.display='block';
  }
}
</script>
</body>
</html>`;

const outDir = path.join(baseDir, 'deploy');
fs.mkdirSync(outDir, {recursive: true});
fs.writeFileSync(path.join(outDir, 'index.html'), lockPage);
console.log('OK: ' + outDir + '/index.html (' + lockPage.length + ' bytes)');
