import {test} from 'node:test';
import assert from 'node:assert/strict';
import {DatabaseSync} from 'node:sqlite';
import {readFileSync,mkdtempSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createApi,createStore} from './api.mjs';
import worker from './worker.mjs';

function database(path=':memory:'){
 const db=new DatabaseSync(path);db.exec(readFileSync(new URL('./schema.sql',import.meta.url),'utf8'));
 const binding={prepare:sql=>({bind:(...values)=>({first:()=>db.prepare(sql).get(...values),all:()=>({results:db.prepare(sql).all(...values)}),run:()=>db.prepare(sql).run(...values)})})};
 return {db,binding};
}
const fixture=(visibility='public')=>({schemaVersion:1,boss:'Training target',startedUtc:'2026-10-01T00:00:00Z',durationSeconds:60,visibility,players:[{name:'Player 01',className:'Templar',damage:90000},{name:'Player 02',className:'Sorcerer',damage:180000}]});
const request=(path,method='GET',input,token)=>new Request('https://thegoatmeter.us'+path,{method,headers:{...(input?{'Content-Type':'application/json'}:{}),...(token?{Authorization:'Bearer '+token}:{})},...(input?{body:JSON.stringify(input)}:{})});
const setup=t=>{const {db,binding}=database();t.after(()=>db.close());return {db,api:createApi(createStore(binding)),binding};};

test('retries recover the same receipt without duplicate scores; changed payload cannot reuse the key',async t=>{
 const {api,db}=setup(t),token='a'.repeat(64),input=fixture();
 const original=await api(request('/api/fights','POST',input,token));assert.equal(original.status,201);const receipt=await original.json();
 const retry=await api(request('/api/fights','POST',input,token));assert.equal(retry.status,200);assert.deepEqual({...await retry.json(),replayed:undefined},{...receipt,replayed:undefined});
 assert.equal(db.prepare('SELECT COUNT(*) AS n FROM fights').get().n,1);
 assert.equal((await api(request('/api/fights','POST',{...input,boss:'Other boss'},token))).status,409);
 const raw=db.prepare('SELECT * FROM fights').get();assert.notEqual(raw.owner_hash,token);assert.notEqual(raw.share_hash,receipt.shareToken);assert.equal(raw.payload.includes(token),false);
});
test('owner can change visibility; share tokens cannot mutate and private records never leak to catalog',async t=>{
 const {api}=setup(t),receipt=await (await api(request('/api/fights','POST',fixture('private')))).json();
 assert.deepEqual((await (await api(request('/api/catalog'))).json()).bosses,[]);
 assert.equal((await api(request('/api/fights/'+receipt.id,'PATCH',{visibility:'public'},receipt.shareToken))).status,404);
 assert.equal((await api(request('/api/fights/'+receipt.id,'PATCH',{visibility:'public',verification:'verified'},receipt.ownerToken))).status,400);
 assert.equal((await api(request('/api/fights/'+receipt.id,'PATCH',{visibility:'public'},receipt.ownerToken))).status,200);
 assert.deepEqual((await (await api(request('/api/catalog'))).json()).bosses,['Training target']);
 assert.equal((await (await api(request('/api/leaderboard?mode=community'))).json()).rows.length,2);
 assert.equal((await api(request('/api/fights/'+receipt.id,'PATCH',{visibility:'unlisted'},receipt.ownerToken))).status,200);
 assert.equal((await api(request('/api/fights/'+receipt.id))).status,404);
 assert.equal((await api(request('/api/fights/'+receipt.id,'GET',null,receipt.shareToken))).status,200);
 assert.equal((await (await api(request('/api/leaderboard?mode=community'))).json()).rows.length,0);
});
test('DPS is computed server-side, filters and pagination are stable, private records excluded',async t=>{
 const {api}=setup(t);await api(request('/api/fights','POST',fixture()));await api(request('/api/fights','POST',{...fixture('private'),boss:'Hidden'}));
 const first=await (await api(request('/api/leaderboard?mode=community&limit=1'))).json();assert.equal(first.rows[0].dps,3000);assert.equal(first.nextOffset,1);
 const second=await (await api(request('/api/leaderboard?mode=community&limit=1&offset=1'))).json();assert.equal(second.rows[0].dps,1500);assert.equal(second.nextOffset,null);
 const cls=await (await api(request('/api/leaderboard?mode=community&class=Templar'))).json();assert.equal(cls.rows.length,1);
 const hidden=await (await api(request('/api/leaderboard?mode=community&boss=Hidden'))).json();assert.equal(hidden.rows.length,0);
 for(const query of ['limit=0','limit=101','offset=-1','mode=forged'])assert.equal((await api(request('/api/leaderboard?'+query))).status,400);
 assert.equal((await (await api(request('/api/leaderboard'))).json()).rows.length,0);
});
test('database itself rejects a forged verified ranking',t=>{
 const {db}=setup(t);assert.throws(()=>db.prepare('INSERT INTO fights VALUES (?,?,?,?,?,?,?)').run('id','date','public','verified','owner','share','{}'),/CHECK/);
});
test('capacity is bounded even for concurrent uploads and reads remain available',async t=>{
 const {binding}=setup(t),api=createApi(createStore(binding),{capacity:1});
 const responses=await Promise.all([api(request('/api/fights','POST',fixture())),api(request('/api/fights','POST',fixture()))]);
 assert.deepEqual(responses.map(r=>r.status).sort(),[201,503]);assert.equal((await api(request('/api/health'))).status,200);
});
test('privacy and upload receipt survive closing and reopening the database',async()=>{
 const dir=mkdtempSync(join(tmpdir(),'goatmeter-api-')),path=join(dir,'test.sqlite');let db;
 try{
  let opened=database(path);db=opened.db;let api=createApi(createStore(opened.binding));const receipt=await (await api(request('/api/fights','POST',fixture('private')))).json();db.close();db=null;
  opened=database(path);db=opened.db;api=createApi(createStore(opened.binding));
  assert.equal((await api(request('/api/fights/'+receipt.id))).status,404);assert.equal((await api(request('/api/fights/'+receipt.id,'GET',null,receipt.ownerToken))).status,200);
 }finally{db?.close();rmSync(dir,{recursive:true,force:true});}
});
test('worker fails closed without database or rate limiter, and serves only archive assets',async t=>{
 assert.equal((await worker.fetch(request('/api/health'),{})).status,503);
 const {binding}=setup(t);assert.equal((await worker.fetch(request('/api/fights','POST',fixture()),{DB:binding,UPLOADS_ENABLED:'false'})).status,503);
 assert.equal((await worker.fetch(request('/api/fights','POST',fixture()),{DB:binding,UPLOADS_ENABLED:'true'})).status,429);
 const response=await worker.fetch(request('/logs/'),{ASSETS:{fetch:async()=>new Response('<h1>Archive</h1>')}});
 assert.equal(response.status,200);assert.equal(response.headers.get('Referrer-Policy'),'no-referrer');
 assert.equal((await worker.fetch(request('/'),{})).status,404);
});

test('character names and known servers survive upload and ranking without merging same-name players',async t=>{
 const {api}=setup(t),input=fixture();
 input.players=[{name:'TestHero',serverId:1102,className:'Templar',damage:90000},{name:'TestHero',serverId:1202,className:'Sorcerer',damage:180000}];
 const response=await api(request('/api/fights','POST',input));assert.equal(response.status,201);const receipt=await response.json();
 const detail=await (await api(request('/api/fights/'+receipt.id))).json();assert.deepEqual(detail.players.map(p=>[p.name,p.serverId]),[['TestHero',1102],['TestHero',1202]]);
 const ranking=await (await api(request('/api/leaderboard?mode=community'))).json();assert.equal(ranking.rows.length,2);assert.deepEqual(ranking.rows.map(p=>p.serverId),[1202,1102]);assert.ok(ranking.rows.every(p=>p.name==='TestHero'&&p.verification==='unverified'));
 input.players[1].serverId=1102;assert.equal((await api(request('/api/fights','POST',input))).status,400);
 input.players[1].serverId=42;assert.equal((await api(request('/api/fights','POST',input))).status,400);
});
