/** Local observed-schedule estimate; never a forward rotation/gear prediction. */
export function estimateReplay(bundle){
 const finite=n=>typeof n==='number'&&Number.isFinite(n)&&n>=0;
 if(!bundle||bundle.schemaVersion!==1||bundle.kind!=='capture-conditioned-replay'||typeof bundle.contextId!=='string'||!bundle.contextId||!Number.isSafeInteger(bundle.durationMs)||bundle.durationMs<1||bundle.durationMs>86400000)throw new Error('Invalid replay context');
 if(bundle.restrictions?.allowGearScaling!==false||bundle.restrictions?.allowNewRotation!==false||bundle.restrictions?.independentValidation!==false)throw new Error('Empirical replay cannot authorize extrapolation or verification');
 if(!Array.isArray(bundle.models)||bundle.models.length>10000||!Array.isArray(bundle.timeline)||bundle.timeline.length>100000)throw new Error('Replay bounds exceeded');
 const models=new Map();for(const m of bundle.models){if(typeof m.id!=='string'||!m.id||models.has(m.id)||typeof m.skill!=='string'||!finite(m.meanObservedHit)||!Number.isSafeInteger(m.trainingHits)||m.trainingHits<5)throw new Error('Invalid hit model');models.set(m.id,m);}
 let predicted=0,last=-1;const bySkill=new Map();
 for(const hit of bundle.timeline){if(!Number.isSafeInteger(hit.atMs)||hit.atMs<last||hit.atMs<0||hit.atMs>=bundle.durationMs||!models.has(hit.modelId))throw new Error('Invalid observed hit schedule');last=hit.atMs;const m=models.get(hit.modelId);predicted+=m.meanObservedHit;bySkill.set(m.skill,(bySkill.get(m.skill)??0)+m.meanObservedHit);}
 const o=bundle.observed;if(!o||!finite(o.coveredDamage)||!finite(o.totalDamage)||o.coveredDamage>o.totalDamage||!Number.isSafeInteger(o.coveredHits)||o.coveredHits!==bundle.timeline.length||!Number.isSafeInteger(o.totalHits)||o.totalHits<o.coveredHits||!o.totalHits||!Number.isFinite(predicted))throw new Error('Invalid observed totals');
 const seconds=bundle.durationMs/1000;
 const d=bundle.diagnostics;
 if(d&&(!finite(d.sumAbsoluteHitError)||!Array.isArray(d.excluded)||d.excluded.some(x=>typeof x.skill!=='string'||!['unseen-signature','insufficient-training'].includes(x.reason)||!Number.isSafeInteger(x.hits)||x.hits<0||!finite(x.damage))))throw new Error('Invalid replay diagnostics');
 if(d&&(d.excluded.reduce((n,x)=>n+x.hits,0)!==o.totalHits-o.coveredHits||d.excluded.reduce((n,x)=>n+x.damage,0)!==o.totalDamage-o.coveredDamage))throw new Error('Inconsistent replay exclusions');
 const diagnostics={coveredHitWeightedAbsoluteErrorPercent:d&&o.coveredDamage?d.sumAbsoluteHitError/o.coveredDamage*100:null,excluded:d?.excluded??null};
 const near=(a,b)=>Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b));
 if(d?.skills!==undefined){
  if(!Array.isArray(d.skills)||d.skills.length>10000)throw new Error('Invalid skill diagnostics');
  const seen=new Set();let hits=0,covered=0,total=0,damage=0,error=0;
  diagnostics.skills=d.skills.map(s=>{
   if(typeof s.skill!=='string'||seen.has(s.skill)||![s.heldOutHits,s.coveredHits,s.knownFlagCoveredHits].every(n=>Number.isSafeInteger(n)&&n>=0)||s.coveredHits>s.heldOutHits||s.knownFlagCoveredHits>s.coveredHits||![s.totalObservedDamage,s.coveredObservedDamage,s.predictedCoveredDamage,s.sumAbsoluteHitError].every(finite)||s.coveredObservedDamage>s.totalObservedDamage||!near(s.predictedCoveredDamage,bySkill.get(s.skill)??0))throw new Error('Invalid skill diagnostic totals');
   seen.add(s.skill);hits+=s.heldOutHits;covered+=s.coveredHits;total+=s.totalObservedDamage;damage+=s.coveredObservedDamage;error+=s.sumAbsoluteHitError;
   return {skill:s.skill,coveredHits:s.coveredHits,heldOutHits:s.heldOutHits,knownFlagCoveredHits:s.knownFlagCoveredHits,damageCoverage:s.totalObservedDamage?s.coveredObservedDamage/s.totalObservedDamage:null,absoluteHitErrorPercent:s.coveredObservedDamage?s.sumAbsoluteHitError/s.coveredObservedDamage*100:null};
  });
  if(hits!==o.totalHits||covered!==o.coveredHits||!near(total,o.totalDamage)||!near(damage,o.coveredDamage)||!near(error,d.sumAbsoluteHitError))throw new Error('Inconsistent skill diagnostics');
 }
 return {...diagnostics,kind:bundle.kind,warning:'CAPTURE-CONDITIONED ESTIMATE — NOT NEW ROTATION OR GEAR DPS',contextId:bundle.contextId,className:bundle.className,observedWindowSeconds:seconds,predictedCoveredDamage:predicted,observedCoveredDamage:o.coveredDamage,predictedCoveredWindowDps:predicted/seconds,observedCoveredWindowDps:o.coveredDamage/seconds,coveredHitFraction:o.coveredHits/o.totalHits,coveredDamageFraction:o.totalDamage?o.coveredDamage/o.totalDamage:null,coveredTotalErrorPercent:o.coveredDamage?Math.abs(predicted-o.coveredDamage)/o.coveredDamage*100:null,bySkill:Object.fromEntries(bySkill),gameFormulaValidated:false,allowGearScaling:false,allowNewRotation:false,note:bundle.note};
}
