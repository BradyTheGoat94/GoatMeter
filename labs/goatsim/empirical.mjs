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
 return {kind:bundle.kind,warning:'CAPTURE-CONDITIONED ESTIMATE — NOT NEW ROTATION OR GEAR DPS',contextId:bundle.contextId,className:bundle.className,observedWindowSeconds:seconds,predictedCoveredDamage:predicted,observedCoveredDamage:o.coveredDamage,predictedCoveredWindowDps:predicted/seconds,observedCoveredWindowDps:o.coveredDamage/seconds,coveredHitFraction:o.coveredHits/o.totalHits,coveredDamageFraction:o.totalDamage?o.coveredDamage/o.totalDamage:null,coveredTotalErrorPercent:o.coveredDamage?Math.abs(predicted-o.coveredDamage)/o.coveredDamage*100:null,bySkill:Object.fromEntries(bySkill),gameFormulaValidated:false,allowGearScaling:false,allowNewRotation:false,note:bundle.note};
}
