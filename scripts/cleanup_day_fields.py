"""
Remove os campos 'date' e 'title' de cada ItineraryDay em todos os trip.json.
Rodar apenas após check_day_dates.py confirmar que tudo está OK.
"""
import json, os, sys
sys.stdout.reconfigure(encoding='utf-8')

BASE = r'G:\Meu Drive\Viagens'
FIELDS_TO_REMOVE = ('date', 'title')

patched = 0
skipped = 0

for folder in sorted(os.listdir(BASE)):
    path = os.path.join(BASE, folder, 'trip.json')
    if not os.path.isfile(path):
        skipped += 1
        continue

    with open(path, encoding='utf-8') as f:
        trip = json.load(f)

    changed = False
    for version in trip.get('itineraryVersions', []):
        for day in version.get('itinerary', []):
            for field in FIELDS_TO_REMOVE:
                if field in day:
                    del day[field]
                    changed = True

    if changed:
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(trip, f, ensure_ascii=False, indent=2)
        print(f'  ✓  {trip.get("id", folder)}')
        patched += 1
    else:
        skipped += 1

print(f'\nAtualizados: {patched}  |  Sem alteração: {skipped}')
