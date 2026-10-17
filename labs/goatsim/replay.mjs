/** Offline PRIVATE replay report. No networking, rotation substitution or stat scaling. */
import {readFile,writeFile,stat} from 'node:fs/promises';
import {pathToFileURL} from 'node:url';
import {estimateReplay} from './empirical.mjs';
export async function main(args){
 const [input,output]=args;
 if(!input||args.length>2)throw new Error('Usage: node labs/goatsim/replay.mjs private-bundle.json [private-report.json]');
 if((await stat(input)).size>1024*1024)throw new Error('Replay limit is 1 MiB; raw logs belong in the Python builder');
 const raw=await readFile(input);if(raw.byteLength>1024*1024)throw new Error('Replay limit is 1 MiB');
 const result=JSON.stringify(estimateReplay(JSON.parse(raw)),null,2)+'\n';
 if(output)await writeFile(output,result,{flag:'wx'});else process.stdout.write(result);
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){main(process.argv.slice(2)).catch(e=>{console.error(e.message);process.exitCode=1;});}
