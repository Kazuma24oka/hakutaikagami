from pathlib import Path
import json,hashlib,collections
from build_progress_report import render
R=Path(__file__).resolve().parents[1]
p=json.loads((R/'Docs/Development/ProgressV1/progress.json').read_bytes());d=json.loads((R/p['design_source']).read_bytes());a=json.loads((R/p['acceptance_source']).read_bytes())
ids=[i['id'] for g in d['groups'] for i in g['items']]
assert len(d['groups'])==15 and len(ids)==60 and len(set(ids))==60
assert [i['id'] for i in p['items']]==ids
assert dict(collections.Counter(i['status'] for i in p['items']))==dict(normal=39,isolated=1,foundation=9,design_only=10,management=1)
assert len(a['cases'])==170
for i in p['items']:
 assert i['existing'] and i['remaining'] and i['evidence']
 assert i['planned_acceptance_cases']==sum(c['design_id']==i['id'] for c in a['cases'])
 for f in i['evidence']:assert (R/f).is_file(),f
def check(f,h):assert hashlib.sha256((R/f).read_bytes()).hexdigest()==h,f
for f,h in p['source_hashes'].items():check(f,h)
logic=None;snapshot=None
if p['evidence'].get('current_logic_verification'):
 f=p['evidence']['current_logic_verification'];check(f,p['evidence']['current_logic_verification_sha256'])
 logic=json.loads((R/f).read_bytes())
 if logic.get("remote_probe"):
  binding=json.loads((R/logic["remote_probe"]["binding"]).read_bytes());assert binding["core_sha256"]==logic["binaries"]["Core/bin/Debug/net8.0/HakutaiKagami.Core.dll"]
  assert json.loads((R/logic["remote_probe"]["result"]).read_bytes())["Status"]=="PASSED"
 if logic["scope"]=="headless-custody-successor-local-visit-reconfirmation-release":
  from verify_custody_succession import check as check_logic
 elif logic["scope"]=="headless-local-consenting-hostage-custody-release":
  from verify_hostage_custody import check as check_logic
 elif logic["scope"]=="headless-declared-unmarried-adult-marriage-shared-family-consent":
  from verify_adult_marriage import check as check_logic
 elif logic["scope"]=="headless-adult-adoption-personal-house-separate-implementation":
  from verify_adult_adoption import check as check_logic
 elif logic["scope"]=="headless-directed-known-relations-contextual-consent":
  from verify_character_relations import check as check_logic
 elif logic["scope"]=="headless-five-ability-seven-skill-evidence-limited-growth":
  from verify_character_development import check as check_logic
 elif logic["scope"]=="headless-stock-backed-owned-garrison-defense-duty-physical-reports":
  from verify_treaty_defense import check as check_logic
 elif logic["scope"]=="headless-real-treaty-supply-carrier-custody-physical-retry":
  from verify_treaty_supply import check as check_logic
 elif logic["scope"]=="headless-versioned-multiclause-treaty-termination-and-requests":
  from verify_treaty import check as check_logic
 elif logic["scope"]=="headless-physical-remote-military-command-succession":
  from verify_remote_military_succession import check as check_logic
 elif logic["scope"]=="headless-local-active-and-stored-garrison-command-succession":
  from verify_active_military_succession import check as check_logic
 elif logic["scope"]=="headless-dated-representative-inquiry-direct-reconfirmation-personal-evidence":
  from verify_representative_contact import check as check_logic
 elif logic["scope"]=="headless-signed-truce-confirmation-physical-receipts-failure-and-report-retry":
  from verify_truce_confirmation import check as check_logic
 elif logic["scope"]=="headless-castellan-and-truce-succession":
  from verify_castellan_succession import check as check_logic
 elif logic["scope"]=="headless-post-succession-administration-personal-service-shared-delegation-budgets":
  from verify_steward_succession import check as check_logic
 elif logic["scope"]=="headless-returned-garrison-case-issuer-succession":
  from verify_military_succession import check as check_logic
 elif logic["scope"]=="headless-living-head-office-individual-house-obligations":
  from verify_house_succession import check as check_logic
 elif logic["scope"]=="headless-castellan-administration-separate-custody":
  from verify_castle_administration import check as check_logic
 elif logic["scope"]=="headless-castle-gate-keep-possession-separate-property":
  from verify_castle_handover import check as check_logic
 elif logic["scope"]=="headless-personal-life-food-rest-goals-known-obligations":
  from verify_personal_life import check as check_logic
 elif logic["scope"]=="headless-common-road-retainer-partial-supply-physical-letters":
  from verify_road_convoy import check as check_logic
 elif logic["scope"]=="headless-same-army-road-march-current-assembly-private-rice":
  from verify_army_road import check as check_logic
 elif logic["scope"]=="headless-physical-freight-reports-cancellation-and-local-camp-supply":
  from verify_freight_operations import check as check_logic
 elif logic["scope"]=="headless-delegated-retainer-physical-house-supply":
  from verify_npc_freight import check as check_logic
 elif logic["scope"]=="headless-regional-support-labor-real-house-rice-military-supply":
  from verify_military_economy import check as check_logic
 elif logic["scope"]=="headless-house-funded-physical-procurement-trust-return":
  from verify_house_logistics import check as check_logic
 elif logic["scope"]=="headless-nationwide-regional-ledgers-local-house-procurement":
  from verify_regional_domains import check as check_logic
 elif logic["scope"]=="headless-material-markets-and-local-npc-administration":
  from verify_material_markets import check as check_logic
 elif logic["scope"]=="headless-community-stewardship-and-local-npc-administration":
  from verify_community_stewardship import check as check_logic
 elif logic["scope"]=="headless-village-consultation-and-local-npc-administration":
  from verify_village_consultation import check as check_logic
 elif logic["scope"]=="headless-house-rice-salary-local-consent-debt-reservation-receipt-reliability":
  from verify_house_salary import check as check_logic
 elif logic["scope"]=="headless-local-rice-trade-reservation-travel-delivery-debt-settlement":
  from verify_regional_trade import check as check_logic
 elif logic["scope"]=="headless-three-community-labor-season-food-relief-generation-recovery":
  from verify_community_economy import check as check_logic
 elif logic["scope"]=="headless-old-lord-release-physical-consent-personal-known-notice":
  from verify_retainer_release import check as check_logic
 elif logic["scope"]=="headless-personal-service-bond-independent-membership-debt-release":
  from verify_personal_service import check as check_logic
 elif logic["scope"]=="headless-local-private-inspection-consultation-disclosure-confirmation":
  from verify_cooperation_talk import check as check_logic
 elif logic["scope"]=="headless-private-single-inspection-foreign-retainer-no-recruitment":
  from verify_private_inspection import check as check_logic
 elif logic["scope"]=="headless-private-individual-standdown-payment-expiry-real-muster":
  from verify_private_standdown import check as check_logic
 elif logic["scope"]=="headless-private-contact-dated-firsthand-information-personal-fee":
  from verify_private_information import check as check_logic
 elif logic["scope"]=="headless-versioned-truce-papers-receipt-consent-own-knowledge":
  from verify_truce_negotiation import check as check_logic
 elif logic["scope"]=="headless-personal-companions-agreement-pay-common-tasks-recovery":
  from verify_personal_companions import check as check_logic
 elif logic["scope"]=="headless-regional-seasonal-rice-accounts-generation-recovery":
  from verify_regional_economy import check as check_logic
 elif logic["scope"]=="headless-normal-faction-paper-reaction-sparse-combat-entry":
  from verify_faction_entry import check as check_logic
 elif logic["scope"]=="headless-actual-clash-loss-return-node3d-data":
  from verify_world_clashes import check as check_logic
 elif logic["scope"]=="headless-npc-garrison-common-land-travel-return":
  from verify_npc_armies import check as check_logic
 else:
  from verify_faction_stances import check as check_logic
 check_logic(logic)
 f=p['evidence']['accepted_baseline_snapshot'];check(f,p['evidence']['accepted_baseline_snapshot_sha256'])
 snapshot=json.loads((R/f).read_bytes());assert snapshot['main']=='9762c59994e1fc9b721b1356eb1534343d5e2fca'
 for item in snapshot['files'].values():check(item['snapshot'],item['sha256'])
def historical(f,h):
 if hashlib.sha256((R/f).read_bytes()).hexdigest()==h:return
 assert snapshot is not None and snapshot['files'].get(f,{}).get('sha256')==h,('historical input not preserved',f)
 check(snapshot['files'][f]['snapshot'],h)
for f,h in p['evidence']['runtime_source_hashes'].items():historical(f,h)
assert (R/'Docs/Development/ProgressV1/index.html').read_text(encoding='utf-8')==render()
print('PASS: 60 rows / 15 domains / 170 planned cases; current and historical source scopes separated')

v=json.loads((R/p['evidence']['latest_game_verification']).read_bytes())
check(p['evidence']['latest_game_verification'],p['evidence']['latest_game_verification_sha256'])
if logic:
 assert snapshot['verification_sha256']==p['evidence']['latest_game_verification_sha256']
 for group in ['sources','binaries','proofs']:
  for f,h in v[group].items():historical(f,h)
else:
 new_path=p['evidence'].get('subsequent_core_verification');new=None
 if new_path:
  check(new_path,p['evidence']['subsequent_core_verification_sha256']);new=json.loads((R/new_path).read_bytes())
  if new['scope']=='normal-game-conditional-engagement-delivery':
   from verify_engagement_delivery import check as check_increment
  else:
   from verify_field_engagement import check as check_increment
  check_increment(new)
 for f,h in v['binaries'].items():
  if hashlib.sha256((R/f).read_bytes()).hexdigest()==h:continue
  assert new is not None and f in {'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll'},f
  check(f,new['binaries'][f])
t=p['evidence']['core_test'];check(t['trx'],t['sha256'])
if logic:assert t['passed']==logic['tests']['passed']
if p['evidence'].get('full_core_verification'):
 f=p['evidence']['full_core_verification'];check(f,p['evidence']['full_core_verification_sha256'])
 full=json.loads((R/f).read_bytes());assert full['status']=='PASSED' and full['scope']=='full-core-exact-godot-tested-binaries'
 for group in ['sources','binaries','proofs']:
  for f,h in full[group].items():historical(f,h)
 assert full['failed']==full['skipped']==0
 if logic:assert snapshot['full_core_sha256']==p['evidence']['full_core_verification_sha256']
 else:assert full['passed']==p['evidence']['core_test']['passed']
print('PASS: historical full-Core/Godot evidence preserved; current verification uses its own declared scope')

# Product-scope estimate is separate from test counts and preserves all adopted design items.
from progress_estimate import load as load_estimate,calculate
x=load_estimate();c=calculate(x,d)
assert sum(v['weight'] for v in x['domains'])==100
covered=[g for v in x['domains'] for g in v['groups']]
assert len(covered)==len(set(covered))==15 and set(covered)=={g['id'] for g in d['groups']}
assert set(x['leaves'])==set(ids)
assert all(isinstance(v['percent'],int) and 0<=v['percent']<=100 and v['basis'] for v in x['leaves'].values())
assert set(x['excluded_from_rollup'])=={'BD-01','BD-60'}
assert 0<=c['total']<=100 and c['total']%x['rounding_step']==0
assert x['overall_range'][0]<=c['total']<=x['overall_range'][1]
assert x['focus']['leaf'] in ids and 3<=len(x['focus']['exit'])<=5
assert len(x['roadmap'])==6 and all(v['finish'] and v['why'] and v['status'] for v in x['roadmap'])
from check_approval_batch import check as check_batch
check_batch()
print('PASS: weighted estimate / immutable pre-code evidence-point implementation batch / 6 large, 15 medium, 60 small entries')
