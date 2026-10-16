export const classes = ['Gladiator','Templar','Assassin','Ranger','Sorcerer','Spiritmaster','Cleric','Chanter','Unknown'];
export function validate(x) {
  const fail = message => { throw Object.assign(new Error(message), {status:400}); };
  const keys = (o, allowed) => { if (!o || typeof o !== 'object' || Array.isArray(o) || Object.keys(o).some(k=>!allowed.includes(k))) fail('Unsupported fields'); };
  const str = (s,max) => typeof s === 'string' && s.trim().length>0 && s.length<=max && !/[\u0000-\u001f]/.test(s);
  keys(x,['schemaVersion','boss','startedUtc','durationSeconds','visibility','players','targets','rankingTargetId']);
  if(![1,2].includes(x.schemaVersion)) fail('Unsupported schema version');
  if(x.schemaVersion===1&&(x.targets!==undefined||x.rankingTargetId!==undefined)) fail('Target breakdowns require schema version 2');
  if(!str(x.boss,80)) fail('Boss must be 1–80 characters');
  if(!str(x.startedUtc,40) || !/Z$/.test(x.startedUtc) || !Number.isFinite(Date.parse(x.startedUtc)) || Date.parse(x.startedUtc)>Date.now()+300000) fail('Invalid UTC start time');
  if(!Number.isFinite(x.durationSeconds)||x.durationSeconds<1||x.durationSeconds>7200) fail('Duration must be 1–7200 seconds');
  if(!['private','unlisted','public'].includes(x.visibility)) fail('Choose a visibility');
  if(!Array.isArray(x.players)||x.players.length<1||x.players.length>24) fail('Expected 1–24 players');
  const names=new Set();
  for(const p of x.players){
    keys(p,['name','serverId','className','damage']);
    if(!str(p.name,40)) fail('Character names must be 1–40 characters');
    if(p.serverId!=null&&(!Number.isInteger(p.serverId)||p.serverId<1001||p.serverId>9999)) fail('Invalid server ID');
    const identity=JSON.stringify([p.name.trim(),p.serverId??null]);
    if(names.has(identity)) fail('Duplicate character name on the same or unknown server'); names.add(identity);
    if(!classes.includes(p.className)) fail('Unsupported class');
    if(!Number.isSafeInteger(p.damage)||p.damage<0||p.damage>1e12) fail('Invalid damage');
  }
  if(!x.players.some(p=>p.damage>0)) fail('Encounter has no damage');
  let targets;
  if(x.schemaVersion===2){
    if(!Array.isArray(x.targets)||x.targets.length<1||x.targets.length>64) fail('Expected 1–64 targets');
    const ids=new Set(),sums=x.players.map(()=>0);
    targets=x.targets.map(t=>{
      keys(t,['id','name','durationSeconds','damageByPlayer']);
      if(typeof t.id!=='string'||!/^target-\d{2}$/.test(t.id)||ids.has(t.id)) fail('Invalid or duplicate target ID');ids.add(t.id);
      if(!Number.isFinite(t.durationSeconds)||t.durationSeconds<1||t.durationSeconds>x.durationSeconds) fail('Invalid target duration');
      if(!str(t.name,80)) fail('Target names must be 1–80 characters');
      if(!Array.isArray(t.damageByPlayer)||t.damageByPlayer.length!==x.players.length) fail('Target damage must match the player list');
      t.damageByPlayer.forEach((n,i)=>{if(!Number.isSafeInteger(n)||n<0||n>x.players[i].damage) fail('Invalid target damage');sums[i]+=n;});
      return {id:t.id,name:t.name.trim(),durationSeconds:t.durationSeconds,damageByPlayer:[...t.damageByPlayer]};
    });
    if(sums.some((n,i)=>n>x.players[i].damage)) fail('Target damage exceeds encounter damage');
    const selected=targets.find(t=>t.id===x.rankingTargetId);
    if(!selected||!selected.damageByPlayer.some(n=>n>0)) fail('Select a recorded boss target with captured damage');
  }
  // Stable canonical order makes retries independent of input property order.
  return {schemaVersion:x.schemaVersion,boss:x.boss.trim(),startedUtc:new Date(x.startedUtc).toISOString(),durationSeconds:x.durationSeconds,visibility:x.visibility,players:x.players.map(p=>({name:p.name.trim(),serverId:p.serverId??null,className:p.className,damage:p.damage})),...(targets?{targets,rankingTargetId:x.rankingTargetId}:{})};
}
