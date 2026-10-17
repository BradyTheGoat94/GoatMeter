/** Stable bounded event queue. Same-time ordering is explicit priority then insertion order. */
export class EventQueue {
 #heap=[];#sequence=0;#executed=0;#now=0;
 constructor({maxEvents=200000}={}){if(!Number.isSafeInteger(maxEvents)||maxEvents<1||maxEvents>1000000)throw new Error('Invalid event budget');this.maxEvents=maxEvents;}
 get now(){return this.#now;} get size(){return this.#heap.length;}
 #less(a,b){return a.atMs<b.atMs||(a.atMs===b.atMs&&(a.priority<b.priority||(a.priority===b.priority&&a.sequence<b.sequence)));}
 schedule(atMs,kind,payload=null,priority=0){
  if(!Number.isSafeInteger(atMs)||atMs<this.#now||typeof kind!=='string'||!kind||!Number.isSafeInteger(priority))throw new Error('Invalid event');
  if(this.#heap.length+this.#executed>=this.maxEvents)throw new Error('Event budget exceeded');
  const event={atMs,kind,payload,priority,sequence:this.#sequence++};let i=this.#heap.length;this.#heap.push(event);
  while(i>0){const parent=(i-1)>>1;if(!this.#less(event,this.#heap[parent]))break;this.#heap[i]=this.#heap[parent];i=parent;}this.#heap[i]=event;return event.sequence;
 }
 #pop(){const top=this.#heap[0],last=this.#heap.pop();if(this.#heap.length){let i=0;while(true){let child=i*2+1;if(child>=this.#heap.length)break;if(child+1<this.#heap.length&&this.#less(this.#heap[child+1],this.#heap[child]))child++;if(!this.#less(this.#heap[child],last))break;this.#heap[i]=this.#heap[child];i=child;}this.#heap[i]=last;}return top;}
 runUntil(endMs,handler){if(!Number.isSafeInteger(endMs)||endMs<this.#now||typeof handler!=='function')throw new Error('Invalid run boundary');while(this.#heap.length&&this.#heap[0].atMs<endMs){const event=this.#pop();this.#now=event.atMs;this.#executed++;handler(event,this);}this.#now=endMs;return this.#executed;}
}
