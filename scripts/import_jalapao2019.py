import xlrd, json, os, re, sys, datetime
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2019-08 Jalap' + chr(227) + 'o'
XLS    = FOLDER + r'\Viagem Jalap' + chr(227) + 'o.xls'
PFX    = 'jalapao-2019'

wb = xlrd.open_workbook(XLS)
ws = wb.sheet_by_name('Roteiro')

def xls_date(serial):
    return datetime.datetime(*xlrd.xldate_as_tuple(serial, wb.datemode)[:3])

SUMMARIES = ["Ida", "Jalap" + chr(227) + "o 1", "Jalap" + chr(227) + "o 2",
             "Jalap" + chr(227) + "o 3", "Jalap" + chr(227) + "o 4",
             "Jalap" + chr(227) + "o 5", "Volta"]

TYPE_COLORS = {
    'Transporte': '#4F81BD', 'Refeicao': '#F79646',
    'Hospedagem': '#9BBB59', 'Passeio': '#4BACC6',
}

def col_type(col, text):
    tl = text.lower()
    if any(w in tl for w in ['v' + chr(244) + 'o', 'voo', 'transfer', 'traslado', 'voo ']):
        return 'Transporte'
    if col == 5: return 'Refeicao'
    if col == 11: return 'Hospedagem'
    return 'Passeio'

itinerary = []
aid = 1
for di, row in enumerate(range(3, 10)):   # rows 3-9 = Cerrado Dourado itinerary
    date_val = ws.cell(row, 0).value
    if isinstance(date_val, float):
        date_str = xls_date(date_val).strftime('%Y-%m-%d')
    else:
        date_str = f"2019-08-{22 + di:02d}"

    activities = []
    for col in range(3, 12):              # cols 3-11 = activity columns
        raw_val = ws.cell(row, col).value
        if raw_val is None or str(raw_val).strip() in ('', '-'):
            continue
        text = str(raw_val).strip()
        parts = text.replace('\n', '|').split('|')
        title = parts[0].strip()
        details = ' · '.join(p.strip() for p in parts[1:] if p.strip()) if len(parts) > 1 else None
        if not title:
            continue
        atype = col_type(col, text)
        color = TYPE_COLORS.get(atype, '#4BACC6')
        a = {"id": f"{PFX}-a{aid:02d}", "title": title, "type": atype,
             "color": color, "icon": "",
             "startSlot": col - 3, "durationSlots": 1, "bankRow": 0}
        if details:
            a["details"] = details
        activities.append(a)
        aid += 1

    itinerary.append({"id": f"{PFX}-d{di+1:02d}", "date": date_str,
                      "title": f"Dia {di+1}", "summary": SUMMARIES[di],
                      "activities": activities})

ws_d = wb.sheet_by_name('Dicas')
links, seen_urls, lid = [], set(), 1
maps_url = ""
for r in range(1, ws_d.nrows):
    tv = ws_d.cell(r, 0).value
    uv = ws_d.cell(r, 2).value if ws_d.ncols > 2 else None
    if not tv or not uv:
        continue
    title = str(tv).strip()
    url = re.sub(r'\+[A-Z]\d+$', '', str(uv).strip())
    if not url.startswith('http'):
        continue
    if title == 'Mapa':
        maps_url = url; continue
    if url in seen_urls:
        continue
    seen_urls.add(url)
    links.append({"id": f"{PFX}-l{lid:02d}", "title": title, "url": url})
    lid += 1

def parse_gastos_xls(ws_g, pfx):
    expenses = []; eid = 1
    for r in range(1, ws_g.nrows):
        marker = str(ws_g.cell(r, 0).value or '').strip()
        if marker not in ('>', chr(9658), chr(9654)):
            continue
        title   = str(ws_g.cell(r, 1).value or '').strip()
        etype   = str(ws_g.cell(r, 2).value or '').strip() or None
        obs1    = str(ws_g.cell(r, 3).value or '').strip()
        obs2    = str(ws_g.cell(r, 4).value or '').strip() if ws_g.ncols > 4 else ''
        obs3    = str(ws_g.cell(r, 5).value or '').strip() if ws_g.ncols > 5 else ''
        price_v  = ws_g.cell(r, 6).value   # col G = preço unitário
        people_v = ws_g.cell(r, 8).value   # col I = pessoas
        qty_v    = ws_g.cell(r, 9).value   # col J = qtd
        paid_v   = ws_g.cell(r, 11).value  # col L = pago

        price  = float(price_v)  if isinstance(price_v,  (int, float)) else 0.0
        people = int(round(float(people_v))) if isinstance(people_v, (int, float)) else 1
        qty    = int(qty_v)    if isinstance(qty_v,    (int, float)) else 1
        paid   = float(paid_v) if isinstance(paid_v,   (int, float)) else 0.0

        if not title:
            title = obs1 if obs1 and obs1 != '-' else (obs2 if obs2 and obs2 != '-' else 'Item')
        obs_parts = [p for p in [obs1, obs2, obs3] if p and p not in ('-', '')]
        notes = ' . '.join(obs_parts) or None

        if etype == 'Hotel': etype = 'Hospedagem'
        if etype in ('Atividades', 'Passeios e Ingressos'): etype = 'Passeios'
        if not etype:
            tl = title.lower()
            if any(w in tl for w in ['hotel', 'pousada', 'pouso', 'glamping']):
                etype = 'Hospedagem'
            elif any(w in tl for w in ['v' + chr(244) + 'o', 'voo', 'traslado', 'transfer', 'aluguel']):
                etype = 'Transporte'
            elif any(w in tl for w in ['ingresso', 'cachoeira', 'trilha', 'jalap', 'parque', 'dunas']):
                etype = 'Passeios'

        e = {"id": f"{pfx}-e{eid:02d}", "isActive": True, "title": title}
        if etype: e["type"] = etype
        if notes: e["notes"] = notes
        e.update({"price": round(price, 2), "taxes": 0.0, "people": people, "quantity": qty,
                  "currency": "BRL", "exchangeRateToBase": 1.0, "paidAmount": round(paid, 2)})
        expenses.append(e); eid += 1
    return expenses

expenses = parse_gastos_xls(wb.sheet_by_name('Gastos'), PFX)

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER, f))])
attachments = [{"id": f"{PFX}-att{i+1:02d}", "file": f} for i, f in enumerate(files)]

trip = {
    "schemaVersion": 1, "id": PFX, "title": "2019-08 Jalap" + chr(227) + "o",
    "startDate": "2019-08-22", "endDate": "2019-08-28",
    "baseCurrency": "BRL", "people": 2, "rateDecimalDigits": 2, "myMapsUrl": maps_url,
    "itinerarySlotsPerDay": 7,
    "itineraryVersions": [{"id": f"{PFX}-v1", "name": "Vers" + chr(227) + "o 1", "bankRows": 3,
                           "itinerary": itinerary, "bankActivities": []}],
    "activeVersionId": f"{PFX}-v1",
    "tasks": [], "links": links, "expenses": expenses, "currencyRates": [], "attachments": attachments
}

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f:
    json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
