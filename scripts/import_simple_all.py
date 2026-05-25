import os, json, re, unicodedata

SKIP = {"2026-05-Vale do Pati", "2026-04 Chapada dos Veadeiros"}
ROOT = r"G:\Meu Drive\Viagens"

def slugify(text):
    text = unicodedata.normalize('NFD', text)
    text = ''.join(c for c in text if unicodedata.category(c) != 'Mn')
    text = text.lower()
    text = re.sub(r'[^a-z0-9]+', '-', text)
    return text.strip('-')

def parse_date(folder):
    m = re.match(r'^(\d{4})-(\d{2})', folder)
    if m:
        return f"{m.group(1)}-{m.group(2)}-01"
    return None

created = []
skipped = []

for folder in sorted(os.listdir(ROOT)):
    path = os.path.join(ROOT, folder)
    if not os.path.isdir(path):
        continue
    if folder in SKIP:
        skipped.append(folder)
        continue
    trip_path = os.path.join(path, 'trip.json')
    if os.path.exists(trip_path):
        skipped.append(folder + " (já existe)")
        continue

    date = parse_date(folder)
    if not date:
        date = "2000-01-01"

    files = sorted([
        f for f in os.listdir(path)
        if not f.startswith('.') and os.path.isfile(os.path.join(path, f))
    ])
    attachments = [{"id": f"att-{i+1:02d}", "file": f} for i, f in enumerate(files)]

    trip_id = slugify(folder)

    trip = {
        "schemaVersion": 1,
        "id": trip_id,
        "title": folder,
        "startDate": date,
        "endDate": date,
        "baseCurrency": "BRL",
        "people": 2,
        "rateDecimalDigits": 2,
        "myMapsUrl": "",
        "itinerarySlotsPerDay": 9,
        "itineraryVersions": [{
            "id": f"{trip_id}-v1",
            "name": "Versão 1",
            "itinerary": [],
            "bankActivities": []
        }],
        "activeVersionId": f"{trip_id}-v1",
        "tasks": [],
        "links": [],
        "expenses": [],
        "currencyRates": [],
        "attachments": attachments
    }

    with open(trip_path, 'w', encoding='utf-8') as f:
        json.dump(trip, f, ensure_ascii=False, indent=2)
    created.append(folder)

print(f"Criados: {len(created)}")
for f in created:
    print(f"  ✓ {f}")
print(f"\nIgnorados: {len(skipped)}")
for f in skipped:
    print(f"  - {f}")
