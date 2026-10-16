import http from 'node:http';
import {Readable} from 'node:stream';
import {DatabaseSync} from 'node:sqlite';
import {readFileSync,mkdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {createApi,createStore,securityHeaders} from './api.mjs';
export function createApp(database=':memory:',options={}){
 const db=new DatabaseSync(database);db.exec(readFileSync(new URL('./schema.sql',import.meta.url),'utf8'));
 const sqlite={prepare:sql=>({bind:(...values)=>({first:()=>db.prepare(sql).get(...values),all:()=>({results:db.prepare(sql).all(...values)}),run:()=>db.prepare(sql).run(...values)})})};
 const limits=new Map();
 const api=createApi(createStore(sqlite),{...options,limitUpload:async request=>{
   const ip=request.headers.get('X-Local-Client'),now=Date.now();
   for(const [k,v] of limits)if(v.until<now)limits.delete(k);
   const rate=limits.get(ip)||{count:0,until:now+60000};limits.set(ip,rate);return ++rate.count<=10;
 }});
 const server=http.createServer(async(req,res)=>{
  try{
   const headers=new Headers();for(const [name,value]of Object.entries(req.headers))if(value)headers.set(name,Array.isArray(value)?value.join(','):value);
   headers.set('X-Local-Client',req.socket.remoteAddress);
   const request=new Request(`http://${req.headers.host}${req.url}`,{method:req.method,headers,...(!['GET','HEAD'].includes(req.method)?{body:Readable.toWeb(req),duplex:'half'}:{})});
   const response=await api(request);
   if(response){res.writeHead(response.status,Object.fromEntries(response.headers));return res.end(Buffer.from(await response.arrayBuffer()));}
   const url=new URL(request.url);
   if(req.method==='GET'&&url.pathname==='/'){res.writeHead(302,{Location:'/logs/'});return res.end();}
   const files={'/logs/':'index.html','/logs/app.js':'app.js','/logs/style.css':'style.css','/logs/brand.png':'brand.png'};
   if(req.method==='GET'&&files[url.pathname]){
    const type=url.pathname.endsWith('.js')?'text/javascript':url.pathname.endsWith('.css')?'text/css':url.pathname.endsWith('.png')?'image/png':'text/html';
    res.writeHead(200,{...securityHeaders,'Content-Type':type});return res.end(readFileSync(new URL(`./public/${files[url.pathname]}`,import.meta.url)));
   }
   res.writeHead(404,securityHeaders);res.end('Not found');
  }catch{if(!res.headersSent)res.writeHead(500,securityHeaders);res.end('Internal server error');}
 });
 server.on('close',()=>db.close());return server;
}
if(process.argv[1]===fileURLToPath(import.meta.url)){
 mkdirSync(new URL('./data/',import.meta.url),{recursive:true});
 createApp(fileURLToPath(new URL('./data/fights.sqlite',import.meta.url))).listen(8787,'127.0.0.1',()=>console.log('GoatMeter local preview: http://127.0.0.1:8787/logs/'));
}
