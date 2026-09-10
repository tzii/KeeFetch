"""Compare two independently validated pilots; never accept or arbitrate labels."""
import argparse
import importlib.util
import json
from collections import Counter
from pathlib import Path

spec=importlib.util.spec_from_file_location('census_collector',Path(__file__).with_name('collect-machine-census.py'))
collector=importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)

def compare(kit,first_name='pilot-a',second_name='pilot-b',output_name='pilot-comparison.json'):
    for name in (first_name,second_name,output_name):
        if Path(name).name!=name or name in ('.','..'): raise ValueError('Pilot paths must be simple names inside the kit')
    if first_name==second_name: raise ValueError('Two distinct pilot invocations are required')
    output=kit/output_name
    if output.exists(): raise ValueError('Refusing to overwrite a pilot comparison')
    first,inputs_a=collector.parse_batch(kit/first_name)
    second,inputs_b=collector.parse_batch(kit/second_name)
    if set(first)!=set(second) or inputs_a!=inputs_b:
        raise ValueError('Pilots must inspect identical manifests and unit keys')
    rows=[]
    for key in sorted(first):
        unit,img=inputs_a[key]
        rows.append({'key':key,'input_url':unit['input_url'],'category':unit['category'],
                     'image':img['image'],'agrees':first[key]['label']==second[key]['label'],
                     'pilot_a':first[key],'pilot_b':second[key]})
    disagreements=[row['key'] for row in rows if not row['agrees']]
    report={'status':'awaiting_direct_inspection','pilot_a':first_name,'pilot_b':second_name,'units':len(rows),
            'agreement_count':len(rows)-len(disagreements),'disagreement_keys':disagreements,
            'label_pairs':dict(Counter(row['pilot_a']['label']+' / '+row['pilot_b']['label'] for row in rows)),
            'comparisons':rows}
    output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(f'Compared {len(rows)} units: {len(disagreements)} disagreements. Direct inspection and observations remain required.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--kit',required=True,type=Path)
    parser.add_argument('--first',default='pilot-a');parser.add_argument('--second',default='pilot-b');parser.add_argument('--output',default='pilot-comparison.json')
    args=parser.parse_args();compare(args.kit.resolve(),args.first,args.second,args.output)
