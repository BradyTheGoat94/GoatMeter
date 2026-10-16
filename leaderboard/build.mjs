import {mkdirSync,cpSync,writeFileSync} from 'node:fs';
mkdirSync(new URL('./dist/logs/',import.meta.url),{recursive:true});
cpSync(new URL('./public/',import.meta.url),new URL('./dist/logs/',import.meta.url),{recursive:true});
writeFileSync(new URL('./dist/_headers',import.meta.url),`/logs/*
  X-Content-Type-Options: nosniff
  Referrer-Policy: no-referrer
  Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'
`);
console.log('Built local assets under leaderboard/dist/logs. Nothing deployed.');
