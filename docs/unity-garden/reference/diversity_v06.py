#!/usr/bin/env python3
"""G0 design reference, not shipped game code.

Run from any directory with Python 3:
    python docs/unity-garden/reference/diversity_v06.py --verify
    python docs/unity-garden/reference/diversity_v06.py --write

--verify recomputes and compares the committed report without writing files.
--write changes only content/diversity-reference-report.v0.6.json.
Imports have no output-file side effects. Requires only the Python standard library.
The numeric kernel is derived from the v0.3/v0.4 design reference, not an
independent implementation. Pinned profile hashes reject coefficient drift.
"""
import argparse
import collections
import copy
import hashlib
import itertools
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / 'content'
PROFILE_PATH = OUT / 'simulation-profile.v0.2.json'
MAP_PATH = OUT / 'garden-map.v0.3.json'
WP_PATH = OUT / 'weather-profile.v0.4.json'
TP_PATH = OUT / 'target-profile.v0.6.json'
P = json.loads(PROFILE_PATH.read_text())
MAP = json.loads(MAP_PATH.read_text())
WP = json.loads(WP_PATH.read_text())
TP = json.loads(TP_PATH.read_text())
W = H = 16
Q = 1000
N = 256
RECIPES = [r['recipeId'] for r in P['recipes']]
BY_RECIPE = {r['recipeId']: r for r in P['recipes']}
FAMILIES = ['water', 'wind', 'light', 'seed']
OFFSETS = [(0,-1),(1,0),(0,1),(-1,0)]
SITE_ORDER = TP['siteOrder']
SITE_BY = {s['siteId']: s for s in TP['sites']}
POLICIES = TP['validation']['pairedCohortPolicies']
SEEDS = TP['validation']['fixtureSeeds']
ROUNDS = TP['validation']['roundsPerSeedAndPolicy']
CHECKPOINTS = TP['validation']['checkpointRounds']
WORDER = WP['weatherOrder']
WBY = {w['weatherId']: w for w in WP['weathers']}
RAIN_OFFSETS = set(WP['rain']['tickOffsets'])
DERIVED_FROM = {'garden_reference_v03.py': '184e2863cb6eb5d8e1bad892e5f4f67f5c7e2ae53bd7b2ceb4944456603dc133', 'weather_reference_v04.py': '7d06475f01d5ead869b384eecbeb5e45692cb7d8c29ee6573602c756f4ebafc5'}

def hash_bytes(b): return hashlib.sha256(b).hexdigest()

def numeric_digest(rows):
    # Only ASCII integer/null arrays, a strict canonical-JSON subset; no floats.
    return hash_bytes(json.dumps(rows,separators=(',',':'),ensure_ascii=True).encode('ascii'))

def cid(x,y): return y*W+x

def xy(i): return [i%W,i//W]

def dist(a,b): return abs(a%W-b%W)+abs(a//W-b//W)

def mf(m):
    if m<=100 or m>=950: return 0
    if m<400: return (m-100)*1000//300
    if m<=700: return 1000
    return (950-m)*1000//250

def expand(m):
    cells=[]
    overrides={o['cellId']:o for o in m['cellOverrides']}
    for i in range(N):
        x,y=xy(i); t=m['cellTypes'][m['terrainRows'][y][x]]
        d={'cellId':i,'height':m['heightRows'][y][x],**t,**m['stateDefaults']}
        d.update(overrides.get(i,{})); d['meanLight']=500*d['lightExposureQ']//1000
        cells.append(d)
    return cells

CELLS=expand(MAP)
STATIC_MATRIX=[[c['cellId'],c['height'],int(c['solid']),c['soilCapacity'],c['infiltrationPerTick'],int(c['plantable']),c['lightExposureQ'],c['lightOccluderCellId'],c['initialSoilWater'],c['initialSurfaceWater'],c['evaporationRemainder'],c['meanLight']] for c in CELLS]
NEIGH=[]
for i in range(N):
    x,y=xy(i); ns=[]
    for rank,(dx,dy) in enumerate(OFFSETS):
        xx,yy=x+dx,y+dy
        if 0<=xx<W and 0<=yy<H and not CELLS[cid(xx,yy)]['solid']:ns.append((cid(xx,yy),rank))
    NEIGH.append(ns)
SOLID=[c['solid'] for c in CELLS]
CAP=[c['soilCapacity'] for c in CELLS]
INF=[c['infiltrationPerTick'] for c in CELLS]
HEIGHT=[c['height'] for c in CELLS]
EXPOSURE=[c['lightExposureQ'] for c in CELLS]
BASE_LIGHT=[500*x//Q for x in EXPOSURE]
OUTLET=MAP['drain']['cellId']
STAGES=P['growth']['transitions']
PROBES={o['id']:o['cellId'] for o in MAP['observationPoints']}
OBS_EDGES={(e['fromCellId'],e['toCellId']):e['id'] for e in MAP['observationEdges']}
SHELTER=[]; BLOCKER=[]
for i in range(N):
    x,y=xy(i); block=None
    for xx in range(x-1,-1,-1):
        j=cid(xx,y)
        if SOLID[j]:block=j;break
    BLOCKER.append(block);SHELTER.append(250 if block is not None else 1000)


RAIN_TARGETS=[i for i in range(N) if not SOLID[i]]
SOIL_IDS=[i for i in range(N) if CELLS[i]['plantable']]

for ref in [TP['baseProfile'],TP['mapReuse'],TP['weatherProfile']]:
    assert hash_bytes((OUT/ref['file']).read_bytes()) == ref['sha256'], ref['file']
assert MAP['expandedCellsEncoding']['sha256'] == numeric_digest(STATIC_MATRIX)
assert SITE_ORDER == ['inlet','shade','meadow']
assert [SITE_BY[s]['centerCellId'] for s in SITE_ORDER] == [85,135,154]

class World:
    def __init__(self,case_id='diversity-reference',weather_id='clear',site_id=None):
        self.surface=[c['initialSurfaceWater'] for c in CELLS]
        self.soil=[c['initialSoilWater'] for c in CELLS]
        self.evap=[0]*N;self.mean_light=list(BASE_LIGHT)
        self.plants={p['cellId']:dict(p) for p in MAP['initialPlants']}
        self.tick=0;self.round=0;self.case_id=case_id
        self.site_id=site_id
        self.set_weather(weather_id)

    def snapshot_hash(self):
        # Fixed ordered numeric state plus cell identity for plants; initial IDs are metadata.
        return numeric_digest([[i,self.surface[i],self.soil[i],self.evap[i],self.mean_light[i],self.plants[i]['growth'] if i in self.plants else None,self.plants[i]['growthRemainder'] if i in self.plants else None] for i in range(N)]+[[self.tick,self.round]])

    def _base_params(self,rid):
        rec=BY_RECIPE[rid];a=rec['authoringParameters']; fam=rec['elementId']; anchor=SITE_BY[self.site_id]['centerCellId'] if self.site_id is not None else MAP['anchors'][a['targetRule']['anchorId']]['cellId']
        schedule={}; field=[0]*N; targets=[]
        if fam=='water':
            targets=[anchor] if rid=='water_drop' else sorted([anchor]+[j for j,_ in NEIGH[anchor]])
            assert len(targets)>=a['targetRule']['minEligibleCells']
            for item in a['injectionSchedule']:
                q,rem=divmod(item['amount'],len(targets))
                schedule[item['tickOffset']]=[(j,q+(k<rem)) for k,j in enumerate(targets)]
        elif fam in ('wind','light'):
            for i in range(N):
                d=dist(i,anchor)
                if SOLID[i] or d>a['radius']:continue
                k=(a['radius']+1-d)*Q//(a['radius']+1)
                targets.append(i)
                if fam=='wind':field[i]=(a['strengthQ']*k//Q)*SHELTER[i]//Q
                else:field[i]=(min(Q,500+a['extraLightQ']*k//Q)*EXPOSURE[i]//Q)-BASE_LIGHT[i]
        else:
            band=a['targetRule'];candidates=[i for i in range(N) if CELLS[i]['plantable'] and i not in self.plants and band['minDistance']<=dist(i,anchor)<=band['maxDistance']]
            candidates.sort(key=lambda i:(dist(i,anchor),i))
            if len(candidates)<a['seedCount']:return None
            targets=candidates[:a['seedCount']];schedule[a['tickOffset']]=[(i,1) for i in targets]
        return {'recipeId':rid,'elementId':fam,'targets':targets,'schedule':schedule,'field':field,'anchor':anchor}

    def set_weather(self,weather_id):
        self.weather_id=weather_id;self.weather=WBY[weather_id]
        self.weather_light=[0 if SOLID[i] else self.weather['baseLightQ']*EXPOSURE[i]//Q for i in range(N)]
        self.weather_wind=[0 if SOLID[i] else self.weather['baseWindQ']*SHELTER[i]//Q for i in range(N)]

    def params(self,rid):
        params=self._base_params(rid)
        if params is None:return None
        fam=params['elementId'];a=BY_RECIPE[rid]['authoringParameters']
        light=list(self.weather_light);wind=list(self.weather_wind);delta=[0]*N
        if fam in ('light','wind'):
            for i in params['targets']:
                k=(a['radius']+1-dist(i,params['anchor']))*Q//(a['radius']+1)
                if fam=='light':
                    raw=min(Q,self.weather['baseLightQ']+a['extraLightQ']*k//Q)
                    light[i]=raw*EXPOSURE[i]//Q;delta[i]=light[i]-self.weather_light[i]
                else:
                    raw=min(Q,self.weather['baseWindQ']+a['strengthQ']*k//Q)
                    wind[i]=raw*SHELTER[i]//Q;delta[i]=wind[i]-self.weather_wind[i]
        params['field']=delta;params['totalLight']=light;params['totalWind']=wind
        return params

    def weights(self):
        result={};details={}
        for rid in RECIPES:
            p=self.params(rid)
            if p is None:continue
            fam=p['elementId'];a=BY_RECIPE[rid]['authoringParameters'];pairs=[]
            if fam=='seed':
                band=a['targetRule']
                support=[i for i in range(N) if CELLS[i]['plantable'] and band['minDistance']<=dist(i,p['anchor'])<=band['maxDistance']]
                pairs=[(i,1,1000 if i not in self.plants else 0) for i in support]
            else:
                doses=collections.Counter()
                if fam=='water':
                    for row in p['schedule'].values():
                        for i,amt in row:doses[i]+=amt
                else:doses.update({i:p['field'][i]*40 for i in p['targets']})
                for i,dose in sorted(doses.items()):
                    if dose<=0 or CAP[i]<=0:continue
                    moisture=self.soil[i]*Q//CAP[i]
                    if fam=='water':need=max(0,400-moisture)*Q//400
                    elif fam=='wind':need=max(0,moisture-700)*Q//300
                    else:need=max(0,Q-self.weather_light[i])
                    pairs.append((i,dose,min(Q,need)))
            den=sum(d for _,d,_ in pairs);nq=sum(d*n for _,d,n in pairs)//den if den else 0
            weight=100+(200*nq+500)//1000
            result[rid]=weight
            details[rid]={'weight':weight,'localNeedQ':nq,'supportCellIds':[i for i,_,_ in pairs]}
        return result,details

    def resolve(self,rid):
        params=self.params(rid)
        assert params is not None, ('ineligible recipe',rid)
        self.round+=1;round_id=f'{self.case_id}:round:{self.round}';fam=params['elementId'];before_soil=list(self.soil)
        before_growth=sum(p['growth'] for p in self.plants.values());before_water=sum(self.surface)+sum(self.soil)
        total={'injected':0,'absorbed':0,'surfaceEvaporated':0,'soilEvaporated':0,'drained':0,'transport':0,'positiveEdgeTickCount':0,'maxSurfaceDepth':max(self.surface),'stages':[],'seedCells':[]}
        absorbed_cell=[0]*N;max_surface_cell=list(self.surface);observed_edges=collections.Counter()
        light=params['totalLight']
        wind=params['totalWind']
        total['weatherInjected']=0;total['diceInjected']=0
        total['weatherId']=self.weather_id
        for k in range(40):
            tick_before=sum(self.surface)+sum(self.soil);inj=0
            weather_inj=0
            if k in RAIN_OFFSETS and self.weather['rainPerTargetAtScheduledTick']:
                for i in RAIN_TARGETS:
                    self.surface[i]+=self.weather['rainPerTargetAtScheduledTick'];weather_inj+=self.weather['rainPerTargetAtScheduledTick']
            total['weatherInjected']+=weather_inj;inj+=weather_inj
            if k in params['schedule']:
                for i,amount in params['schedule'][k]:
                    if fam=='water':self.surface[i]+=amount;inj+=amount;total['diceInjected']+=amount
                    elif fam=='seed':
                        assert i not in self.plants and CELLS[i]['plantable']
                        self.plants[i]={'plantId':f'{round_id}:seed:{i}','cellId':i,'growth':0,'growthRemainder':0,'stage':'seed','originKind':'seedInput','seedSourceRoundId':round_id}
                        total['seedCells'].append(i)
            total['injected']+=inj
            assert total['diceInjected']<=1200 and total['weatherInjected']<=2560 and total['injected']<=3760
            total['maxSurfaceDepth']=max(total['maxSurfaceDepth'],max(self.surface))
            assert all(0<=v<=1000000 for v in self.surface)
            changes=[0]*N;heads=[HEIGHT[i]+self.surface[i] for i in range(N)]
            for i,w in enumerate(self.surface):
                if not w or SOLID[i]:continue
                req=[]
                for j,rank in NEIGH[i]:
                    amount=max(0,heads[i]-heads[j])*125//1000
                    if amount:req.append((j,amount,rank))
                r=sum(x[1] for x in req)
                if not r:continue
                b=min(w,r);values=[amount*b//r for _,amount,_ in req]
                left=b-sum(values)
                order=sorted(range(len(req)),key=lambda ix:(-(req[ix][1]*b%r),req[ix][2]))
                for ix in order[:left]:values[ix]+=1
                assert sum(values)==b<=w
                for (j,request,_),amount in zip(req,values):
                    assert amount<=request
                    changes[i]-=amount;changes[j]+=amount
                    total['transport']+=amount;total['positiveEdgeTickCount']+=bool(amount)
                    if (i,j) in OBS_EDGES:observed_edges[OBS_EDGES[(i,j)]]+=amount
            self.surface=[self.surface[i]+changes[i] for i in range(N)]
            out=min(self.surface[OUTLET],max(0,self.surface[OUTLET]-150),100)
            self.surface[OUTLET]-=out;total['drained']+=out
            evap_surface=evap_soil=0
            for i in range(N):
                if SOLID[i]:continue
                absorb=min(self.surface[i],CAP[i]-self.soil[i],INF[i])
                self.surface[i]-=absorb;self.soil[i]+=absorb;absorbed_cell[i]+=absorb;total['absorbed']+=absorb
                water=self.surface[i]+self.soil[i]
                if not water:self.evap[i]=0;continue
                demand,rem=divmod(self.evap[i]+light[i]+4*wind[i],Q)
                actual=min(demand,water);es=min(actual,self.surface[i]);et=actual-es
                self.surface[i]-=es;self.soil[i]-=et;self.evap[i]=rem if water>actual else 0
                evap_surface+=es;evap_soil+=et
                assert 0<=self.soil[i]<=CAP[i] and 0<=self.surface[i]<=1000000
                max_surface_cell[i]=max(max_surface_cell[i],self.surface[i])
            total['surfaceEvaporated']+=evap_surface;total['soilEvaporated']+=evap_soil
            self.tick+=1
            for i,p in self.plants.items():
                old=p['growth'];m=self.soil[i]*Q//CAP[i]
                increment,rem=divmod(p['growthRemainder']+25*mf(m)*light[i],1000000)
                p['growth']=min(2500,old+increment);p['growthRemainder']=0 if p['growth']==2500 else rem
                p['stage']=next((t['stage'] for t in reversed(STAGES) if p['growth']>=t['threshold']),'seed')
                assert 0<=p['growth']<=2500 and 0<=p['growthRemainder']<1000000
                for t in STAGES:
                    if old<t['threshold']<=p['growth']:total['stages'].append({'cellId':i,'kind':t['event'],'tick':self.tick})
            after=sum(self.surface)+sum(self.soil)
            assert all(0<=v<1000 for v in self.evap)
            assert after-tick_before==inj-out-evap_surface-evap_soil,(rid,k,'water balance')
        self.mean_light=light
        after_water=sum(self.surface)+sum(self.soil)
        assert after_water-before_water==total['injected']-total['drained']-total['surfaceEvaporated']-total['soilEvaporated']
        total['roundId']=round_id;total['waterBalanceResidual']=0;total['totalWaterAfter']=after_water
        total['surfaceWaterAfter']=sum(self.surface);total['soilWaterAfter']=sum(self.soil)
        total['plantGrowthAfter']=sum(p['growth'] for p in self.plants.values());total['growthDelta']=total['plantGrowthAfter']-before_growth
        total['flowerCount']=sum(p['growth']==2500 for p in self.plants.values());total['plantCount']=len(self.plants)
        total['soilChangeCells']=sum(a!=b for a,b in zip(before_soil,self.soil))
        total['maxPostTickSurfaceDepth']=max(max_surface_cell)
        total['observationEdgeTransfers']=[observed_edges[e['id']] for e in MAP['observationEdges']]
        total['probes']={name:{'soilBefore':before_soil[i],'soilAfter':self.soil[i],'absorbed':absorbed_cell[i],'surfaceAfter':self.surface[i],'maxPostTickSurface':max_surface_cell[i],'effectiveLightQ':light[i],'effectiveWindQ':wind[i],'growthAfter':self.plants[i]['growth'] if i in self.plants else None} for name,i in PROBES.items()}
        total['totalInjected']=total['injected']
        assert total['totalInjected']==total['weatherInjected']+total['diceInjected']
        total['minSoilWater']=min(self.soil[i] for i in range(N) if CAP[i]>0)
        total['maxSoilWater']=max(self.soil[i] for i in range(N) if CAP[i]>0)
        for name,i in PROBES.items():
            total['probes'][name]['weatherLightQ']=self.weather_light[i]
            total['probes'][name]['weatherWindQ']=self.weather_wind[i]
            total['probes'][name]['diceAdditionalLightQ']=light[i]-self.weather_light[i]
            total['probes'][name]['diceAdditionalWindQ']=wind[i]-self.weather_wind[i]
        return total

class Buffer:
    def __init__(self,seed,index,role,length):
        index_name='weatherPeriod' if role=='weather' else 'round'
        self.data=b''.join(hashlib.sha256(f'quiet-basin-v04|seed={seed}|{index_name}={index}|role={role}|block={b}'.encode('ascii')).digest() for b in range((length+31)//32))[:length]
        self.offset=0;self.rejected=0

    def below(self,n):
        if n==1:return 0
        assert 1<n<2**32
        limit=2**32-(2**32%n)
        while self.offset+4<=len(self.data):
            x=int.from_bytes(self.data[self.offset:self.offset+4],'big');self.offset+=4
            if x<limit:return x%n
            self.rejected+=1
        raise ValueError('fixture buffer exhausted')

    def d6(self):
        while self.offset<len(self.data):
            x=self.data[self.offset];self.offset+=1
            if x<252:return x%6+1
            self.rejected+=1
        raise ValueError('fixture buffer exhausted')

def choose(items,weights,buffer):
    threshold=buffer.below(sum(weights[x] for x in items))
    for x in items:
        if threshold<weights[x]:return x
        threshold-=weights[x]
    raise AssertionError('weighted draw')

def mapping(world,buffer):
    weights,details=world.weights()
    families={fam:[r for r in RECIPES if r in weights and BY_RECIPE[r]['elementId']==fam] for fam in FAMILIES}
    eligible=[f for f in FAMILIES if families[f]];assert len(eligible)>=3
    fw={f:sum(weights[r] for r in families[f])//len(families[f]) for f in eligible}
    selected=[]
    for _ in range(3):
        f=choose([f for f in eligible if f not in selected],fw,buffer);selected.append(f)
    faces=[choose(families[f],weights,buffer) for f in selected]
    for _ in range(3):
        counts=collections.Counter(faces);fcounts=collections.Counter(BY_RECIPE[r]['elementId'] for r in faces)
        candidates=[r for r in RECIPES if r in weights and counts[r]<2 and fcounts[BY_RECIPE[r]['elementId']]<3]
        faces.append(choose(candidates,weights,buffer))
    for i in range(5,0,-1):
        j=buffer.below(i+1);faces[i],faces[j]=faces[j],faces[i]
    assert max(collections.Counter(faces).values())<=2
    fcounts=collections.Counter(BY_RECIPE[r]['elementId'] for r in faces)
    assert len(fcounts)>=3 and max(fcounts.values())<=3
    return faces,details

def select_weather(seed,period):
    buffer=Buffer(seed,period,'weather',32)
    weights=WP['episodes']['initialWeights'] if period==0 else WP['episodes']['laterWeights']
    bucket=buffer.below(256);choice=bucket
    for weather_id,weight in zip(WORDER,weights):
        if choice<weight:break
        choice-=weight
    else:raise AssertionError('weather selection')
    assert buffer.offset==4 and buffer.rejected==0
    return {'weatherPeriodIndex':period,'selectionKind':WP['episodes']['initialSelectionKind'] if period==0 else WP['episodes']['laterSelectionKind'],'weatherId':weather_id,'weights':weights,'bucket':bucket,'bytesUsed':4,'rawBytesHex':buffer.data.hex()}

# The following comparisons were specified before cohort execution. They never
# feed back into world state, target choice, mapping, or fixture material.
STAGE_ORDER = ['seed', 'sprout', 'leaf', 'bud', 'flower']
REPORT_PATH = OUT / 'diversity-reference-report.v0.6.json'
ROUND_COLUMNS = ['siteIndex','drawnSiteIndex','faceRecipes','dieFace','cardFace',
                 'chosenRecipeIndex','seedCells','stageEvents']
STAGE_EVENT_ORDER = [t['event'] for t in STAGES]
PAIR_COLUMNS = ['seed','policyA','policyB','round','exactDifferent','coarseDifferent',
                'soilWaterL1','surfaceWaterL1','growthL1','occupiedSymmetricDifference']


def exact_state(world):
    """Actual ordered integers, excluding every identifier and history label."""
    return tuple((world.surface[i],world.soil[i],world.evap[i],world.mean_light[i],
                  world.plants[i]['growth'] if i in world.plants else None,
                  world.plants[i]['growthRemainder'] if i in world.plants else None)
                 for i in range(N))


def coarse_state(world):
    # Fixed physical categories, not a random seed/hash or cosmetic distinction.
    # 0=dry stop, 1=below optimal, 2=optimal, 3=above optimal, 4=wet stop.
    def band(i):
        moisture=world.soil[i]*Q//CAP[i]
        return 0 if moisture<=100 else 1 if moisture<400 else 2 if moisture<=700 else 3 if moisture<950 else 4
    return (tuple(band(i) for i in SOIL_IDS),
            tuple((i,STAGE_ORDER.index(world.plants[i]['stage'])) for i in sorted(world.plants)),
            tuple(i for i in range(N) if not SOLID[i] and world.surface[i]>=120))


def coarse_payload(value):
    return {'soilMoistureBands':''.join(map(str,value[0])),
            'plantCellStageRows':[list(row) for row in value[1]],
            'puddleCellIds':list(value[2])}


def checkpoint(world):
    exact=exact_state(world);coarse=coarse_state(world)
    return {'round':world.round,'numericStateSha256':numeric_digest(exact),
            'coarsePhysicalState':coarse_payload(coarse),
            'soilWater':sum(world.soil),'surfaceWater':sum(world.surface),
            'plantGrowth':sum(p['growth'] for p in world.plants.values())},(exact,coarse)


def seed_support():
    initial={p['cellId'] for p in MAP['initialPlants']};union=set();sites=[]
    for site in SITE_ORDER:
        center=SITE_BY[site]['centerCellId'];bands={}
        for rid in ['seed_single','seed_scatter']:
            rule=BY_RECIPE[rid]['authoringParameters']['targetRule']
            cells=[i for i in SOIL_IDS if rule['minDistance']<=dist(i,center)<=rule['maxDistance']]
            bands[rid]={'cellIds':cells,'initiallyOccupied':sorted(set(cells)&initial),
                        'initiallyEmpty':sorted(set(cells)-initial)}
        cells=set(bands['seed_single']['cellIds'])|set(bands['seed_scatter']['cellIds'])
        union.update(cells)
        sites.append({'siteId':site,'bands':bands,'unionCount':len(cells),
                      'staticPlantCountUpperBound':len(initial|cells)})
    return {'plantableSoilCellIds':SOIL_IDS,'sites':sites,'unionCellIds':sorted(union),
            'unionCount':len(union),'initialPlantsInUnion':len(initial&union),
            'additionalEmptyCellsInUnion':len(union-initial),
            'staticPlantCountUpperBound':len(initial|union),
            'outsideUnionCellIds':sorted(set(SOIL_IDS)-union),
            'meaning':'Static upper bound only. Greedy seed selection, scatter needing two empty cells, chosen recipes and site history may leave cells unoccupied. This is not a demonstrated maximum or a normal-play guarantee.'}


def assert_uniform_prelude():
    checks=[]
    for word,expected in [(0,0),(1,1),(2,2),(4294967294,2)]:
        b=Buffer(0,1,'mapping',256);b.data=word.to_bytes(4,'big')+b.data[4:]
        actual=b.below(3);assert actual==expected and b.offset==4
        checks.append([word,actual,b.offset,b.rejected])
    b=Buffer(0,1,'mapping',256);b.data=(4294967295).to_bytes(4,'big')+(4).to_bytes(4,'big')+b.data[8:]
    assert b.below(3)==1 and b.offset==8 and b.rejected==1
    checks.append([4294967295,1,b.offset,b.rejected])
    used=[]
    for preference in ['auto']+SITE_ORDER:
        b=Buffer(0,1,'mapping',256);draw=b.below(3)
        selected=SITE_ORDER[draw] if preference=='auto' else preference
        used.append({'preference':preference,'drawnSiteIndex':draw,'selectedSiteId':selected,'bytesUsed':b.offset})
    assert len({r['bytesUsed'] for r in used})==1
    b=Buffer(0,1,'mapping',256);b.data=b'\xff'*256
    try:b.below(3)
    except ValueError:pass
    else:raise AssertionError('all rejected words must exhaust')
    assert b.offset==256 and b.rejected==64
    return {'columns':['firstWord','resultIndex','bytesUsed','rejections'],'rows':checks,
            'fixedPreferenceConsumption':used,'allRejectedExhaustion':{'bytesUsed':256,'rejectedWords':64,'newMaterialFetched':False}}


def old_anchor_regression():
    old=json.loads((OUT/'weather-reference-report.v0.4.json').read_text())
    rows=[]
    keys=['weatherInjected','diceInjected','totalInjected','absorbed','surfaceEvaporated',
          'soilEvaporated','drained','transport','stages','seedCells','totalWaterAfter',
          'surfaceWaterAfter','soilWaterAfter','plantGrowthAfter','growthDelta','flowerCount','plantCount']
    for case in old['singleCases']:
        rid=case['round']['recipeId'];world=World('legacy-regression',case['weatherId'])
        result=world.resolve(rid)
        assert world.snapshot_hash()==case['finalStateNumericHash'],case['id']
        for key in keys:assert result[key]==case['round'][key],(case['id'],key)
        rows.append([case['weatherId'],rid])
    assert len(rows)==24
    return {'caseCount':24,'weatherRecipeRows':rows,'numericHashesAndSelectedQuantitiesMatched':True,
            'scope':'Original four anchors only; derived kernel regression, not independent physics verification.'}


def single_cases():
    cases=[];states={}
    for weather in WORDER:
        for site in SITE_ORDER:
            for rid in RECIPES:
                world=World(f'single-{weather}-{site}-{rid}',weather,site)
                parameters=world.params(rid);assert parameters is not None
                weights=world.weights()[0];result=world.resolve(rid);cp,state=checkpoint(world)
                case={'id':f'{weather}/{site}/{rid}','weatherId':weather,'siteId':site,'recipeId':rid,
                      'targetCells':parameters['targets'],'initialRecipeWeight':weights[rid],
                      'weatherInjected':result['weatherInjected'],'diceInjected':result['diceInjected'],
                      'totalWaterAfter':result['totalWaterAfter'],'seedCells':result['seedCells'],
                      'stageEvents':[[e['cellId'],e['kind'],e['tick']] for e in result['stages']],
                      'siteSoilWaterAfter':[world.soil[SITE_BY[s]['centerCellId']] for s in SITE_ORDER],
                      'plantCellGrowthRows':[[i,p['growth']] for i,p in sorted(world.plants.items())],
                      'numericStateSha256':cp['numericStateSha256']}
                cases.append(case);states[(weather,site,rid)]=state
    pairs=[]
    for weather in WORDER:
        for rid in ['water_drop','water_rain','seed_single','seed_scatter']:
            for a,b in itertools.combinations(SITE_ORDER,2):
                sa=states[(weather,a,rid)];sb=states[(weather,b,rid)]
                pairs.append([weather,rid,a,b,sa[0]!=sb[0],sa[1]!=sb[1]])
    # Actual water/seed input differs in every same-weather, same-recipe site pair.
    assert len(pairs)==36 and all(p[4] for p in pairs)
    return cases,{'columns':['weather','recipe','siteA','siteB','exactDifferent','coarseDifferent'],
                  'rows':pairs,'exactDifferentCount':sum(p[4] for p in pairs),
                  'coarseDifferentCount':sum(p[5] for p in pairs)}


def order_cases():
    cases=[];comparisons=[]
    for site in SITE_ORDER:
        values=[]
        for sequence in [('seed_single','light_soft'),('light_soft','seed_single')]:
            world=World(f'order-{site}','clear',site);steps=[]
            for rid in sequence:
                r=world.resolve(rid);steps.append({'recipeId':rid,'seedCells':r['seedCells'],
                      'plantGrowthAfter':r['plantGrowthAfter'],'totalWaterAfter':r['totalWaterAfter']})
            cp,state=checkpoint(world);values.append(state)
            cases.append({'siteId':site,'weatherId':'clear','recipeSequence':list(sequence),'rounds':steps,
                          'finalPlantCellGrowthRows':[[i,p['growth']] for i,p in sorted(world.plants.items())],
                          'finalState':cp})
        comparisons.append([site,values[0][0]!=values[1][0],values[0][1]!=values[1][1]])
    return {'sequenceCaseCount':6,'roundResolutionCount':12,'cases':cases,
            'comparisonColumns':['siteId','exactDifferent','coarseDifferent'],'comparisonRows':comparisons,
            'meaning':'Same two inputs and 80 ticks, reversed order. Clear weather is forced for this controlled natural-model fixture; no normal journal or oracle events are generated.'}


def policy_preference(policy,round_no):
    return SITE_ORDER[((round_no-1)//3)%3] if policy=='cycle_three' else policy


def run_fixture(seed,policy):
    world=World(f'{policy}-seed-{seed}');rows=[];checkpoints=[];saved={};materials=hashlib.sha256();periods=[];mapping_max_bytes=0
    first_bloom=None;totals=collections.Counter();prelude_rejections=0
    for round_no in range(1,ROUNDS+1):
        if world.round%3==0:
            selected=select_weather(seed,world.round//3);world.set_weather(selected['weatherId'])
            periods.append(selected['weatherId']);materials.update(bytes.fromhex(selected['rawBytesHex']))
        mb=Buffer(seed,round_no,'mapping',256);ob=Buffer(seed,round_no,'outcome',32);cb=Buffer(seed,round_no,'oracle',32)
        for buffer in [mb,ob,cb]:materials.update(buffer.data)
        drawn=mb.below(3);prelude_rejections+=mb.rejected
        preference=policy_preference(policy,round_no)
        site=SITE_ORDER[drawn] if preference=='auto' else preference
        world.site_id=site
        faces,_=mapping(world,mb);die=ob.d6();card=cb.d6();rid=faces[die-1]
        result=world.resolve(rid)
        rows.append([SITE_ORDER.index(site),drawn,''.join(str(RECIPES.index(r)) for r in faces),
                     die,card,RECIPES.index(rid),result['seedCells'],
                     [[e['cellId'],STAGE_EVENT_ORDER.index(e['kind']),e['tick']] for e in result['stages']]])
        mapping_max_bytes=max(mapping_max_bytes,mb.offset)
        if first_bloom is None and result['flowerCount']:first_bloom=round_no
        for k in ['weatherInjected','diceInjected','totalInjected','drained','surfaceEvaporated','soilEvaporated']:
            totals[k]+=result[k]
        totals['maxSurfaceDepth']=max(totals['maxSurfaceDepth'],result['maxSurfaceDepth'])
        if round_no in CHECKPOINTS:
            cp,state=checkpoint(world);checkpoints.append(cp);saved[round_no]=state
    return {'seed':seed,'policy':policy,'materialSha256':materials.hexdigest(),'weatherPeriods':periods,
            'roundRows':rows,'checkpoints':checkpoints,'firstBloomRound':first_bloom,
            'totals':dict(totals),'sitePreludeRejectedWords':prelude_rejections,'mappingMaxBytesUsed':mapping_max_bytes},saved


def pair_difference(a,b):
    exacta,coarsea=a;exactb,coarseb=b
    return [exacta!=exactb,coarsea!=coarseb,
            sum(abs(x[1]-y[1]) for x,y in zip(exacta,exactb)),
            sum(abs(x[0]-y[0]) for x,y in zip(exacta,exactb)),
            sum(abs((x[4] or 0)-(y[4] or 0)) for x,y in zip(exacta,exactb)),
            len({i for i,x in enumerate(exacta) if x[4] is not None}^{i for i,x in enumerate(exactb) if x[4] is not None})]


def collision_groups(items):
    groups=collections.defaultdict(list)
    for label,value in items:groups[value].append(label)
    return [labels for labels in groups.values() if len(labels)>1]


def cohort():
    runs=[];states={}
    for seed in SEEDS:
        current=[]
        for policy in POLICIES:
            r,s=run_fixture(seed,policy);runs.append(r);current.append(r);states[(seed,policy)]=s
        # Material equality is checked before interpreting policy differences.
        assert len({r['materialSha256'] for r in current})==1
        assert len({tuple(r['weatherPeriods']) for r in current})==1
        assert len({tuple((row[3],row[4]) for row in r['roundRows']) for r in current})==1
        print(f'seed {seed}: all five policies complete',flush=True)
    pairs=[]
    for seed in SEEDS:
        for a,b in itertools.combinations(POLICIES,2):
            for n in CHECKPOINTS:pairs.append([seed,a,b,n]+pair_difference(states[(seed,a)][n],states[(seed,b)][n]))
    summary=[]
    for n in CHECKPOINTS:
        exact=[(f'{seed}/{policy}',states[(seed,policy)][n][0]) for seed in SEEDS for policy in POLICIES]
        coarse=[(f'{seed}/{policy}',states[(seed,policy)][n][1]) for seed in SEEDS for policy in POLICIES]
        summary.append({'round':n,'caseCount':len(exact),'distinctExactStates':len({x[1] for x in exact}),
                        'distinctCoarseStates':len({x[1] for x in coarse}),
                        'exactCollisionGroups':collision_groups(exact),'coarseCollisionGroups':collision_groups(coarse)})
    auto_unique=len({states[(seed,'auto')][24][1] for seed in SEEDS})
    fixed=[]
    for a,b in itertools.combinations(SITE_ORDER,2):
        for n in [12,24]:
            count=sum(states[(seed,a)][n][1]!=states[(seed,b)][n][1] for seed in SEEDS)
            fixed.append({'policyA':a,'policyB':b,'round':n,'differentCoarseStates':count,'seedCount':len(SEEDS),'minimum':8,'met':count>=8})
    triples=[{'round':n,'allThreeCoarseDistinctSeedCount':sum(len({states[(seed,site)][n][1] for site in SITE_ORDER})==3 for seed in SEEDS),'seedCount':len(SEEDS),'minimum':8} for n in [12,24]]
    for row in triples:row['met']=row['allThreeCoarseDistinctSeedCount']>=row['minimum']
    repeat,repeat_states=run_fixture(0,'auto')
    expected=next(r for r in runs if r['seed']==0 and r['policy']=='auto')
    assert repeat==expected and repeat_states==states[(0,'auto')]
    criteria={'autoAtRound24':{'distinctCoarseStates':auto_unique,'caseCount':len(SEEDS),'minimum':8,'met':auto_unique>=8},
              'fixedSitePairs':fixed,'fixedSiteTriples':triples,'allMet':auto_unique>=8 and all(x['met'] for x in triples),
              'meaning':'Predeclared G2 arithmetic candidate criteria only. No seed exclusion, outcome adjustment, rendering quality judgement or uniqueness guarantee.'}
    return {'seeds':SEEDS,'policies':POLICIES,'roundsPerCase':ROUNDS,'allRunsReported':True,
            'noRerollOrPostResultTuning':True,'roundRowColumns':ROUND_COLUMNS,
            'roundRowNumber':'Index+1, starting from round1','stageEventRowColumns':['cellId','stageEventCode','tick'],
            'stageEventCodeOrder':STAGE_EVENT_ORDER,'runs':runs,
            'sameMaterialPairColumns':PAIR_COLUMNS,'sameMaterialPairRows':pairs,
            'physicalDiversityAtCheckpoints':summary,'candidateAcceptance':criteria,
            'exactReplay':{'seed':0,'policy':'auto','roundCount':ROUNDS,'allRecordedValuesAndCheckpointStatesMatched':True}}


def pretty(value,level=0):
    """Keep physical rows compact while preserving readable object boundaries."""
    indent='  '*level
    if isinstance(value,dict) and level>=3:
        return json.dumps(value,ensure_ascii=False,separators=(',',':'))
    if isinstance(value,dict):
        return '{\n'+',\n'.join('  '*(level+1)+json.dumps(k,ensure_ascii=False)+': '+pretty(v,level+1) for k,v in value.items())+'\n'+indent+'}'
    if isinstance(value,list) and value and any(isinstance(v,dict) for v in value):
        return '[\n'+',\n'.join('  '*(level+1)+pretty(v,level+1) for v in value)+'\n'+indent+']'
    return json.dumps(value,ensure_ascii=False,separators=(',',':'))


def make_report():
    prelude=assert_uniform_prelude();regression=old_anchor_regression()
    single,controlled_pairs=single_cases();order=order_cases();sampled=cohort()
    return {'documentKind':'diversity-reference-design-report','schemaVersion':'diversity-reference.v0.6',
            'status':'designCandidate','implementationStatus':'notImplementedInUnity','date':'2026-11-11',
            'targetProfileFile':TP_PATH.name,'targetProfileSha256':hash_bytes(TP_PATH.read_bytes()),
            'referenceScriptFile':'../reference/diversity_v06.py','referenceScriptSha256':hash_bytes(Path(__file__).read_bytes()),
            'inheritedReferenceScriptHashes':DERIVED_FROM,
            'profilePins':[TP['baseProfile'],TP['mapReuse'],TP['weatherProfile']],
            'executedScope':{'legacyAnchorRegressionRounds':24,'siteWeatherRecipeSingleRounds':72,
                'inputOrderRounds':12,'cohortRuns':80,'cohortRounds':1920,'exactReplayRounds':24,
                'totalRoundResolutions':2052,'totalTickUpdates':82080,
                'allTickWaterBalancesZero':True,'allStatesInProfileBounds':True,
                'mappingCapsCheckedForRounds':1944,'fullEventAggregatorExecuted':False,
                'oracleMessageSelectorExecuted':False,'journalOrPresentationExecuted':False},
            'reproduction':{'command':'python docs/unity-garden/reference/diversity_v06.py --verify',
                'writeCommand':'python docs/unity-garden/reference/diversity_v06.py --write',
                'dependency':'Python standard library and pinned repository JSON only; no scratch files or network',
                'startingState':'Every case begins from unchanged garden-map.v0.3. No player names, prayer text, cosmetic labels or initial-state variation enter the natural model.',
                'entropy':'SHA-256 expansion of ASCII quiet-basin-v04|seed={seed}|round={round}|role={mapping/outcome/oracle}|block={block}; weather uses weatherPeriod={period} instead of round={round}. Decimal ASCII, no leading zeros or newline. Buffers mapping256 / other roles32. Materials match across all five policies for each seed.',
                'mapping':'One UniformBelow(3) target prelude, including fixed preferences; then unchanged weighted family/recipe draws and Fisher-Yates from the same buffer cursor.',
                'seedPolicyCycle':'cycle_three is a fixed development input sequence: inlet rounds1..3, shade4..6, meadow7..9, repeating. Every preference is supplied before preparation; no additional player preference enum.',
                'exactState':'Compare actual tuples for cellIds0..255: surfaceWater,soilWater,evaporationRemainder,meanLight,plantGrowthOrNull,plantGrowthRemainderOrNull. Hashes are diagnostic; comparisons are not based on run IDs or hash uniqueness.',
                'coarseState':{'soilCellIdOrder':SOIL_IDS,'moistureBandCodes':{'0':'m<=100','1':'100<m<400','2':'400<=m<=700','3':'700<m<950','4':'m>=950'},'m':'floor(1000*soilWater/soilCapacity)','plantStageCodes':STAGE_ORDER,'puddle':'Final non-solid cell surfaceWater>=120; a state threshold, not a claim that PuddleFormed fired this round.','exclusions':['identifiers','raw state remainders','sub-band moisture differences','random material','message/card','appearance']},
                'waterBalance':'Delta(surface+soil)=weatherInjected+diceInjected-drained-surfaceEvaporated-soilEvaporated, checked at every tick.',
                'cohortCheckpoints':[6,12,24]},
            'seedReachStatic':seed_support(),'targetPreludeChecks':prelude,'legacyRegression':regression,
            'singleCases':single,'sameInputSiteComparisons':controlled_pairs,'inputOrderCases':order,
            'fixtureSampling':sampled,
            'notCovered':['Unity/C# checked Int64 execution','independent second physics implementation','journal durability, queued preference and recovery','complete event aggregation and oracle predicates/selection','actual rendered or audible differences','mobile or Steam performance','initial-state or appearance variation','infinite-horizon stability or uniqueness','statistical effect of intention'],
            'acceptanceMeaning':'Physical reference diversity under fixed materials only; collisions are retained and all cases are reported. No claim of infinite unique gardens, real-player frequency or confirmed visual quality.'}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    mode=parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--verify',action='store_true')
    mode.add_argument('--write',action='store_true')
    args=parser.parse_args()
    # All other repository content files are read-only inputs to this utility.
    before={p.name:hash_bytes(p.read_bytes()) for p in OUT.iterdir() if p.is_file() and p!=REPORT_PATH}
    report=make_report()
    after={p.name:hash_bytes(p.read_bytes()) for p in OUT.iterdir() if p.is_file() and p!=REPORT_PATH}
    assert before==after,'reference changed input content'
    text=pretty(report)+'\n'
    assert len(text.encode('utf-8'))<300000,'keep report under 300KB'
    if args.write:REPORT_PATH.write_text(text)
    else:assert json.loads(REPORT_PATH.read_text())==report,'committed report differs; review before --write'
    print(json.dumps({'mode':'write' if args.write else 'verify','reportBytes':len(text.encode('utf-8')),
                      'rounds':report['executedScope']['totalRoundResolutions'],
                      'ticks':report['executedScope']['totalTickUpdates'],
                      'candidateAcceptance':report['fixtureSampling']['candidateAcceptance'],
                      'allOtherContentUnchanged':True},ensure_ascii=False),flush=True)


if __name__=='__main__':
    main()
