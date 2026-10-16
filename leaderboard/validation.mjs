export const classes = ['Gladiator','Templar','Assassin','Ranger','Sorcerer','Spiritmaster','Cleric','Chanter','Unknown'];
export function validate(x) {
  const fail = message => { throw Object.assign(new Error(message), {status:400}); };
  const keys = (o, allowed) => { if (!o || typeof o !== 'object' || Array.isArray(o) || Object.keys(o).some(k=>!allowed.includes(k))) fail('Unsupported fields'); };
  const str = (s,max) => typeof s === 'string' && s.trim().length>0 && s.length<=max && !/[\u0000-\u001f]/.test(s);
  keys(x,['schemaVersion','boss','startedUtc','durationSeconds','visibility','players']);
  if(x.schemaVersion!==1) fail('Unsupported schema version');
  if(!str(x.boss,80)) fail('Boss must be 1–80 characters');
  if(!str(x.startedUtc,40) || !/Z$/.test(x.startedUtc) || !Number.isFinite(Date.parse(x.startedUtc)) || Date.parse(x.startedUtc)>Date.now()+300000) fail('Invalid UTC start time');
  if(!Number.isFinite(x.durationSeconds)||x.durationSeconds<1||x.durationSeconds>7200) fail('Duration must be 1–7200 seconds');
  if(!['private','unlisted','public'].includes(x.visibility)) fail('Choose a visibility');
  if(!Array.isArray(x.players)||x.players.length<1||x.players.length>24) fail('Expected 1–24 players');
  const names=new Set();
  for(const p of x.players){
    keys(p,['name','className','damage']);
    if(!str(p.name,40)||names.has(p.name)) fail('Player aliases must be unique and 1–40 characters'); names.add(p.name);
    if(!classes.includes(p.className)) fail('Unsupported class');
    if(!Number.isSafeInteger(p.damage)||p.damage<0||p.damage>1e12) fail('Invalid damage');
  }
  if(!x.players.some(p=>p.damage>0)) fail('Encounter has no damage');
  // Stable canonical order makes retries independent of input property order.
  return {schemaVersion:1,boss:x.boss.trim(),startedUtc:new Date(x.startedUtc).toISOString(),durationSeconds:x.durationSeconds,visibility:x.visibility,players:x.players.map(p=>({name:p.name,className:p.className,damage:p.damage}))};
}
