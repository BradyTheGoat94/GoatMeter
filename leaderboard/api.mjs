import {validate} from './validation.mjs';
export const securityHeaders = {'Cache-Control':'no-store','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer','Content-Security-Policy':"default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'"};
const digest = async value => [...new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(value)))].map(b=>b.toString(16).padStart(2,'0')).join('');
const secret = () => [...crypto.getRandomValues(new Uint8Array(32))].map(b=>b.toString(16).padStart(2,'0')).join('');
const error = (message,status=400) => Object.assign(new Error(message),{status});
const json = (status,data) => new Response(JSON.stringify(data),{status,headers:{...securityHeaders,'Content-Type':'application/json'}});

// Shared SQL works with local SQLite and Cloudflare D1. Never interpolate inputs.
export function createStore(db){
 const one=async(sql,...v)=>db.prepare(sql).bind(...v).first();
 const all=async(sql,...v)=>(await db.prepare(sql).bind(...v).all()).results;
 const run=async(sql,...v)=>db.prepare(sql).bind(...v).run();
 return {
  get:id=>one('SELECT * FROM fights WHERE id=?',id),byOwner:key=>one('SELECT * FROM fights WHERE owner_hash=?',key),
  count:()=>one('SELECT COUNT(*) AS total FROM fights'),
  insert:(r,capacity)=>run('INSERT INTO fights(id,created,visibility,verification,owner_hash,share_hash,payload) SELECT ?,?,?,?,?,?,? WHERE (SELECT COUNT(*) FROM fights) < ?',...r,capacity),
  remove:id=>run('DELETE FROM fights WHERE id=?',id),
  visibility:(id,visibility,payload)=>run('UPDATE fights SET visibility=?,payload=? WHERE id=?',visibility,JSON.stringify(payload),id),
  catalog:()=>all("SELECT DISTINCT json_extract(t.value,'$.name') AS boss FROM fights f,json_each(f.payload,'$.targets') t WHERE f.visibility='public' AND json_extract(f.payload,'$.schemaVersion')=2 AND json_extract(t.value,'$.id')=json_extract(f.payload,'$.rankingTargetId') ORDER BY boss"),
  leaderboard:(boss,cls,limit,offset)=>all(`SELECT f.id,json_extract(t.value,'$.name') AS boss,
   json_extract(f.payload,'$.startedUtc') AS startedUtc,json_extract(t.value,'$.durationSeconds') AS durationSeconds,
   json_extract(p.value,'$.name') AS name,json_extract(p.value,'$.serverId') AS serverId,json_extract(p.value,'$.className') AS className,
   json_extract(t.value,'$.damageByPlayer[' || p.key || ']') AS damage,
   CAST(json_extract(t.value,'$.damageByPlayer[' || p.key || ']') AS REAL)/json_extract(t.value,'$.durationSeconds') AS dps,f.verification
   FROM fights f,json_each(f.payload,'$.players') p,json_each(f.payload,'$.targets') t WHERE f.visibility='public' AND f.verification='unverified' AND json_extract(f.payload,'$.schemaVersion')=2 AND json_extract(t.value,'$.id')=json_extract(f.payload,'$.rankingTargetId') AND json_extract(t.value,'$.damageByPlayer[' || p.key || ']')>0
   AND (?='' OR json_extract(t.value,'$.name')=?) AND (?='' OR json_extract(p.value,'$.className')=?)
   ORDER BY dps DESC,f.created DESC,f.id,p.key LIMIT ? OFFSET ?`,boss,boss,cls,cls,limit,offset)
 };
}
async function readJson(request){
 if(!/^application\/json(?:\s*;|$)/i.test(request.headers.get('Content-Type')||''))throw error('JSON required',415);
 if(Number(request.headers.get('Content-Length'))>65536)throw error('Maximum upload size is 64 KiB',413);
 const reader=request.body?.getReader();if(!reader)throw error('Missing upload');const chunks=[];let size=0;
 while(true){const {value,done}=await reader.read();if(done)break;size+=value.byteLength;if(size>65536){await reader.cancel();throw error('Maximum upload size is 64 KiB',413);}chunks.push(value);}
 const bytes=new Uint8Array(size);let offset=0;for(const c of chunks){bytes.set(c,offset);offset+=c.length;}
 try{return JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes));}catch{throw error('Invalid JSON or UTF-8');}
}
export function createApi(store,{limitUpload=async()=>true,capacity=10000,enabled=true}={}){
 return async function handle(request){
  const url=new URL(request.url);if(!url.pathname.startsWith('/api/'))return null;
  try{
   if(url.pathname==='/api/health'&&request.method==='GET')return json(200,{service:'GoatMeter fight archive',schemaVersion:2,verificationAvailable:false,uploadsEnabled:enabled});
   const auth=(request.headers.get('Authorization')||'').replace(/^Bearer /,'');const origin=request.headers.get('Origin');
   if(!['GET','HEAD'].includes(request.method)&&origin&&origin!==url.origin)return json(403,{error:'Cross-origin mutation rejected'});
   if(request.method==='POST'&&url.pathname==='/api/fights'){
    if(!enabled)return json(503,{error:'Uploads are not enabled on this environment'});
    if(!await limitUpload(request))return json(429,{error:'Upload limit reached. Try again in a minute.'});
    // Random client-generated owner keys make a lost-response retry safe. Only hashes persist.
    if(auth&&!/^[a-f0-9]{64}$/.test(auth))return json(400,{error:'Upload key must be 32 random bytes encoded as hexadecimal'});
    const payload=validate(await readJson(request)),ownerToken=auth||secret();
    const shareToken=await digest('GoatMeter/share/v1/'+ownerToken),ownerHash=await digest(ownerToken);
    const existing=await store.byOwner(ownerHash);
    if(existing){if(existing.payload!==JSON.stringify(payload))return json(409,{error:'Upload key already belongs to another encounter'});return json(200,{id:existing.id,verification:'unverified',ownerToken,shareToken,replayed:true});}
    if((await store.count()).total>=capacity)return json(503,{error:'Archive capacity reached. Contact the archive owner.'});
    const id=crypto.randomUUID();
    try{
     const result=await store.insert([id,new Date().toISOString(),payload.visibility,'unverified',ownerHash,await digest(shareToken),JSON.stringify(payload)],capacity);
     if((result.meta?.changes??result.changes)===0)return json(503,{error:'Archive capacity reached. Contact the archive owner.'});
    }
    catch(e){const first=await store.byOwner(ownerHash);if(!first||first.payload!==JSON.stringify(payload))throw e;return json(200,{id:first.id,verification:'unverified',ownerToken,shareToken,replayed:true});}
    return json(201,{id,verification:'unverified',ownerToken,shareToken});
   }
   if(request.method==='GET'&&url.pathname==='/api/catalog')return json(200,{bosses:(await store.catalog()).map(r=>r.boss)});
   if(request.method==='GET'&&url.pathname==='/api/leaderboard'){
    const mode=url.searchParams.get('mode')||'verified';if(!['verified','community'].includes(mode))return json(400,{error:'Invalid ranking mode'});
    const l=url.searchParams.get('limit')||'50',o=url.searchParams.get('offset')||'0';
    if(!/^\d{1,3}$/.test(l)||!/^\d{1,7}$/.test(o)||+l<1||+l>100)return json(400,{error:'Invalid pagination'});
    if(mode==='verified')return json(200,{rows:[],verificationAvailable:false,nextOffset:null});
    const limit=+l,offset=+o,boss=url.searchParams.get('boss')||'',cls=url.searchParams.get('class')||'';
    if(boss.length>80||cls.length>40)return json(400,{error:'Invalid filter'});
    const rows=await store.leaderboard(boss,cls,limit+1,offset);return json(200,{rows:rows.slice(0,limit),verificationAvailable:false,nextOffset:rows.length>limit?offset+limit:null});
   }
   const match=url.pathname.match(/^\/api\/fights\/([a-f0-9-]{36})$/);
   if(match){
    const fight=await store.get(match[1]);if(!fight)return json(404,{error:'Fight not found'});const owner=auth&&await digest(auth)===fight.owner_hash;
    if(request.method==='DELETE'){if(!owner)return json(404,{error:'Fight not found'});await store.remove(fight.id);return json(200,{deleted:true});}
    if(request.method==='PATCH'){
     if(!owner)return json(404,{error:'Fight not found'});const input=await readJson(request);
     if(!input||Object.keys(input).length!==1||!['private','unlisted','public'].includes(input.visibility))return json(400,{error:'Only visibility may be changed'});
     const payload={...JSON.parse(fight.payload),visibility:input.visibility};await store.visibility(fight.id,input.visibility,payload);return json(200,{id:fight.id,visibility:input.visibility,verification:'unverified'});
    }
    if(request.method!=='GET')return json(405,{error:'Method not allowed'});
    if(!owner&&fight.visibility!=='public'&&!(fight.visibility==='unlisted'&&auth&&await digest(auth)===fight.share_hash))return json(404,{error:'Fight not found'});
    return json(200,{id:fight.id,...JSON.parse(fight.payload),verification:fight.verification,createdUtc:fight.created});
   }
   return json(404,{error:'Not found'});
  }catch(e){return json(e.status||500,{error:e.status?e.message:'Internal server error'});}
 };
}
