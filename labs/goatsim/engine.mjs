import {EventQueue} from './events.mjs';
/** Synthetic timing model, not a Global-client or DPS implementation. Times are integer ms. */
export function simulate(config) {
 const {durationMs,skills,lines,steps,delayMs=10,basic=null,manual=[],resource={initial:0,max:0},maxEvents=200000}=config;
 const {executionMode='macro',apl=[],decisionIntervalMs=10}=config;
 const integer=(n,min=0)=>Number.isSafeInteger(n)&&n>=min;
 if(!integer(durationMs,1)||durationMs>600000||![10,20].includes(delayMs)||!integer(maxEvents,1)||maxEvents>1000000)throw new Error('Invalid simulation bounds');
 if(!skills||!['macro','apl'].includes(executionMode))throw new Error('Invalid execution mode');
 if(executionMode==='macro'&&(!lines||!Array.isArray(steps)||!steps.length||steps.length>20))throw new Error('Missing macro configuration');
 if(executionMode==='apl'&&(!Array.isArray(apl)||!apl.length||apl.length>100||!integer(decisionIntervalMs,1)))throw new Error('Invalid APL configuration');
 for(const [id,s] of Object.entries(skills)) {
  if(!id||!integer(s.cooldownMs)||!integer(s.lockMs,1)||!integer(s.cost??0)||!integer(s.gain??0))throw new Error('Every skill requires explicit cooldown and positive execution lock');
 }
 for(const line of Object.values(lines??{}))if(!Array.isArray(line)||line.length>4||line.some(id=>!Object.hasOwn(skills,id)))throw new Error('Invalid hotbar line');
 if(executionMode==='macro'&&steps.some(key=>!Object.hasOwn(lines,key)))throw new Error('Missing hotbar slot');
 for(const rule of apl)if(!objectRule(rule,skills))throw new Error('Invalid APL rule');
 if(!integer(resource.max)||!integer(resource.initial)||resource.initial>resource.max)throw new Error('Invalid resource');
 if(basic&&(!Object.hasOwn(skills,basic.skill)||!integer(basic.intervalMs,1)))throw new Error('Invalid basic input');
 if(!Array.isArray(manual)||manual.length>10000||manual.some(m=>!integer(m.atMs)||!Object.hasOwn(skills,m.skill)))throw new Error('Invalid manual action');
 const actions=manual.map((m,index)=>({...m,index})).sort((a,b)=>a.atMs-b.atMs||a.index-b.index);
 const ready=Object.create(null),counts=Object.create(null),timeline=[];
 let pool=resource.initial,lockedUntil=0,step=0;
 const usable=(id,at)=>at>=lockedUntil&&(ready[id]??0)<=at&&pool>=(skills[id].cost??0);
 function cast(id,at,input){const s=skills[id];pool=Math.min(resource.max,pool-(s.cost??0)+(s.gain??0));ready[id]=at+s.cooldownMs;lockedUntil=at+s.lockMs;counts[id]=(counts[id]??0)+1;timeline.push({atMs:at,skill:id,input,resource:pool});}
 // Tie order is explicit model policy: manual, basic, macro. No input queue or retry for manual taps.
 const queue=new EventQueue({maxEvents});
 const schedule=(at,kind,payload,priority)=>{if(at<durationMs)queue.schedule(at,kind,payload,priority);};
 for(const action of actions)schedule(action.atMs,'manual',action,-2);
 if(basic)schedule(0,'basic',null,-1);
 schedule(0,'decision',null,0);
 const events=queue.runUntil(durationMs,event=>{
  const at=event.atMs;
  if(event.kind==='manual'){const m=event.payload;if(usable(m.skill,at))cast(m.skill,at,'manual');else timeline.push({atMs:at,skill:m.skill,input:'manual',skipped:true});}
  else if(event.kind==='basic'){if(usable(basic.skill,at))cast(basic.skill,at,'basic');schedule(at+basic.intervalMs,'basic',null,-1);}
  else {
   if(executionMode==='macro'){const key=steps[step++%steps.length];const id=lines[key].find(id=>usable(id,at));if(id)cast(id,at,`macro:${key}`);schedule(at+delayMs,'decision',null,0);}
   else {const rule=apl.find(r=>usable(r.skill,at)&&pool>=(r.minResource??0)&&at>=(r.notBeforeMs??0));if(rule)cast(rule.skill,at,'apl');schedule(at+decisionIntervalMs,'decision',null,0);}
  }
 });
 return {executionMode,model:'synthetic-timing-v0',verifiedGameMechanics:false,durationMs,counts,timeline,resource:pool,events};
}

function objectRule(r,skills){return r&&typeof r==='object'&&Object.hasOwn(skills,r.skill)&&Object.keys(r).every(k=>['skill','minResource','notBeforeMs'].includes(k))&&['minResource','notBeforeMs'].every(k=>r[k]===undefined||(Number.isSafeInteger(r[k])&&r[k]>=0));}
