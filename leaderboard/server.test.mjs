import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createApp} from './server.mjs';
import {validate} from './validation.mjs';
const fight=(visibility='public')=>({schemaVersion:2,rankingTargetId:'target-01',targets:[{id:'target-01',name:'Test encounter',durationSeconds:60,damageByPlayer:[6000]}],boss:'Test encounter',startedUtc:'2026-10-01T00:00:00Z',durationSeconds:60,visibility,players:[{name:'Player 01',className:'Cleric',damage:6000}]});
test('strict validation rejects trust flags, identity fields, bad durations and damage',()=>{
 for(const edit of [x=>x.verified=true,x=>x.players[0].characterId=42,x=>x.durationSeconds=0,x=>x.durationSeconds=Infinity,x=>x.players[0].damage=-1,x=>x.players[0].className='Fake',x=>x.players.push({...x.players[0]}),x=>x.startedUtc='tomorrow']){const x=fight();edit(x);assert.throws(()=>validate(x));}
});
test('API enforces visibility, keys, verified exclusion, filters, deletion and rate limit',async t=>{
 const s=createApp();await new Promise(r=>s.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>s.close(r)));
 const base='http://127.0.0.1:'+s.address().port;
 const request=(path,opts)=>fetch(base+path,opts);
 const upload=async visibility=>{const r=await request('/api/fights',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(fight(visibility))});assert.equal(r.status,201);return r.json();};
 const pub=await upload('public'),priv=await upload('private'),unlisted=await upload('unlisted');
 assert.equal((await (await request('/api/leaderboard')).json()).rows.length,0);
 let rows=(await (await request('/api/leaderboard?mode=community')).json()).rows;assert.equal(rows.length,1);assert.equal(rows[0].dps,100);assert.equal(rows[0].verification,'unverified');
 assert.equal((await (await request('/api/leaderboard?mode=community&class=Assassin')).json()).rows.length,0);
 assert.equal((await request('/api/fights/'+priv.id)).status,404);
 assert.equal((await request('/api/fights/'+priv.id,{headers:{Authorization:'Bearer '+priv.shareToken}})).status,404);
 assert.equal((await request('/api/fights/'+priv.id,{headers:{Authorization:'Bearer '+priv.ownerToken}})).status,200);
 assert.equal((await request('/api/fights/'+unlisted.id)).status,404);
 assert.equal((await request('/api/fights/'+unlisted.id,{headers:{Authorization:'Bearer '+unlisted.shareToken}})).status,200);
 assert.equal((await request('/api/fights/'+pub.id,{method:'DELETE'})).status,404);
 assert.equal((await request('/api/fights/'+pub.id,{method:'DELETE',headers:{Authorization:'Bearer '+pub.ownerToken}})).status,200);
 assert.equal((await request('/api/fights/'+pub.id)).status,404);
 const bad=await request('/api/fights',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({...fight(),verification:'verified'})});assert.equal(bad.status,400);
 assert.equal((await request('/api/fights',{method:'POST',headers:{'Content-Type':'application/json',Origin:'https://evil.example'},body:'{}'})).status,403);
 assert.equal((await request('/api/fights',{method:'POST',headers:{'Content-Type':'application/json'},body:'x'.repeat(65537)})).status,413);
 for(let i=0;i<5;i++)await request('/api/fights',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});
 assert.equal((await request('/api/fights',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'})).status,429);
});
