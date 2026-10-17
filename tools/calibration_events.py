"""Stream confirmed diagnostic hits for LOCAL analysis. Returned events contain private partitions;
only aggregate consumers may serialize their outputs. No raw packet bytes or player names returned.
"""
import collections, datetime, hashlib, re

FLAG_NAMES=('crit','parry','perfect','double','back','front')

def logged_flags(fields):
    """Check consistency with the checked-in decoder; this is not client-mechanics validation."""
    try:
        raw_type=int(fields['rawType']);mods=int(fields['mods'],16);direction=int(fields['direction'],16)
        if not 0<=raw_type<=255 or not 0<=mods<=255 or direction not in (0,1,2):return None
        expected=(raw_type==3,bool(mods&2),bool(mods&4),bool(mods&8),direction==1,direction==2)
        if any(fields.get(k) not in ('True','False') for k in FLAG_NAMES):return None
        actual=tuple(fields[k]=='True' for k in FLAG_NAMES)
        return actual if actual==expected else None
    except (KeyError,ValueError):return None

def confirmed_hits(lines):
    pending=collections.OrderedDict();flags=collections.OrderedDict();seen=set()
    for line in lines:
        parts=line.strip().split('|');stamp=parts[0]
        f=dict(x.split('=',1) for x in parts[1:] if '=' in x)
        tag=f.get('tag')
        if tag=='damageFlags':
            key=(stamp,f.get('actor'),f.get('target'),f.get('skill'))
            flags.setdefault(key,collections.deque()).append(logged_flags(f))
            while len(flags)>128:flags.popitem(last=False)
        elif tag in ('damage','dot') and f.get('amount','').isdigit():
            key=(stamp,f.get('src'),f.get('tgt'))
            flag_key=(*key,f.get('skill'))
            values=flags.get(flag_key);hit_flags=values.popleft() if values else None
            if values is not None and not values:flags.pop(flag_key,None)
            pending.setdefault(key,collections.deque()).append((f.get('skill',''),int(f['amount']),tag,f.get('type','unknown'),hashlib.sha256(f.get('raw','').encode()).digest(),hit_flags))
            while len(pending)>128:pending.popitem(last=False)
        elif tag=='resolvedCombat':
            key=(stamp,f.get('entity'),f.get('target'));items=pending.get(key)
            hit=items.popleft() if items else None
            if items is not None and not items:pending.pop(key,None)
            if not hit or f.get('confirmed')!='True' or f.get('class') in (None,'Unknown'):continue
            dedup=(*key,f.get('scope'),*hit)
            if dedup in seen:continue
            seen.add(dedup)
            try:time=datetime.datetime.fromisoformat(re.sub(r'(\.\d{6})\d+',r'\1',stamp).replace('Z','+00:00')).timestamp()
            except ValueError:continue
            yield {'time':time,'class':f['class'],'skill':hit[0],'amount':hit[1],'kind':hit[2],'reportedType':hit[3],'flags':hit[5],
                   'partition':(f.get('scope','unknown'),f.get('entity'),f.get('target'))}
