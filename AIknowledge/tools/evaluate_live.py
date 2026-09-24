"""Explicit opt-in live smoke evaluation with synthetic, non-client examples.

Provider calls incur normal Bailian usage costs. This checks the contract only;
read every output against evaluation_cases.json and obtain clinical review.
"""
import argparse
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--live', action='store_true', help='Make paid provider API calls using configured credentials')
    parser.add_argument('--output', type=Path, required=True, help='Local JSON result file')
    args = parser.parse_args()
    if not args.live:
        parser.error('--live is required to make provider calls')
    sys.path.insert(0, str(ROOT.parent / 'api_backend'))
    os.environ.setdefault('DJANGO_SETTINGS_MODULE', 'sandtray_api.settings')
    import django
    django.setup()
    from django.conf import settings
    from analysis.services import generate_reflection, ReflectionNotConfigured, ReflectionProviderError
    if not settings.BAILIAN_API_KEY:
        parser.error('BAILIAN_API_KEY is not configured; no calls made')
    results = []
    for case in json.loads((ROOT / 'evaluation_cases.json').read_text()):
        inputs = {k: v for k,v in case.items() if k in ('session_data','language','reflection_focus')}
        result = {'id':case['id'], 'review_criteria':case['review'], 'clinical_review':'pending'}
        try:
            result.update(contract_pass=True, result=generate_reflection(**inputs))
        except (ReflectionNotConfigured, ReflectionProviderError) as exc:
            result.update(contract_pass=False, error_type=type(exc).__name__)
        results.append(result)
        args.output.write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n')
        print(case['id'], 'contract passed' if result['contract_pass'] else 'failed', flush=True)
    return 0 if all(r['contract_pass'] for r in results) else 1

if __name__ == '__main__':
    raise SystemExit(main())
