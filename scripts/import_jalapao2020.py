import xlrd, json, os, sys
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2020-05 Jalap' + chr(227) + 'o'
XLS    = FOLDER + r'\Viagem Jalap' + chr(227) + 'o.xls'
PFX    = '2020-05-jalapao'

wb = xlrd.open_workbook(XLS, formatting_info=True)
ws = wb.sheet_by_name('Roteiro')

FIRST_COL   = 4   # 1-indexed
SLOTS_PER_DAY = 7
PEOPLE      = 2

def cell_color(row, col):
    """row, col são 1-indexed; retorna '#RRGGBB'."""
    try:
        xf_idx = ws.cell_xf_index(row - 1, col - 1)
        xf = wb.xf_list[xf_idx]
        ci = xf.background.pattern_colour_index
        rgb = wb.colour_map.get(ci)
        if rgb:
            return '#{:02X}{:02X}{:02X}'.format(*rgb)
    except Exception:
        pass
    return '#000000'

def parse_text(value):
    text = str(value or '').strip()
    if not text or text == '-':
        return None, None
    normalized = text.replace('\n', '|')
    if '|' in normalized:
        parts = [p.strip() for p in normalized.split('|') if p.strip()]
        return parts[0], ' · '.join(parts[1:]) if len(parts) > 1 else ''
    return text, ''

def act(aid, row, col, atype, bankrow=0):
    """row, col são 1-indexed."""
    val = ws.cell_value(row - 1, col - 1)
    title, details = parse_text(val)
    if not title:
        return None
    color = cell_color(row, col)
    a = {"id": aid, "title": title, "type": atype, "color": color, "icon": "",
         "startSlot": col - FIRST_COL, "durationSlots": 1, "bankRow": bankrow}
    if details:
        a["details"] = details
    return a

DATES     = ["2020-05-12","2020-05-13","2020-05-14","2020-05-15",
             "2020-05-16","2020-05-17","2020-05-18"]
SUMMARIES = ["Ida","Jalapão 1","Jalapão 2","Jalapão 3",
             "Jalapão 4","Jalapão 5","Volta"]

DAYS_DEF = [
    (4, 0, [(4,'a01','Transporte'),(6,'a02','Refeicao'),(10,'a03','Hospedagem')]),
    (5, 1, [(4,'a04','Passeio'),(6,'a05','Refeicao'),(7,'a06','Passeio'),(9,'a07','Passeio'),(10,'a08','Hospedagem')]),
    (6, 2, [(4,'a09','Passeio'),(5,'a10','Passeio'),(6,'a11','Refeicao'),(7,'a12','Passeio'),(8,'a13','Passeio'),(9,'a14','Passeio'),(10,'a15','Hospedagem')]),
    (7, 3, [(4,'a16','Passeio'),(5,'a17','Passeio'),(6,'a18','Refeicao'),(7,'a19','Passeio'),(8,'a20','Passeio'),(10,'a21','Hospedagem')]),
    (8, 4, [(4,'a22','Passeio'),(5,'a23','Passeio'),(6,'a24','Refeicao'),(7,'a25','Passeio'),(9,'a26','Passeio'),(10,'a27','Hospedagem')]),
    (9, 5, [(4,'a28','Passeio'),(5,'a29','Passeio'),(6,'a30','Passeio'),(7,'a31','Refeicao'),(8,'a32','Transporte'),(10,'a33','Hospedagem')]),
    (10,6, [(6,'a34','Refeicao'),(7,'a35','Transporte')]),
]
BANK_DEF = [(12, 0, [(4,'b01','Trilha'),(6,'b02','Trilha')])]

itinerary = []
for (row, di, cols) in DAYS_DEF:
    activities = []
    for (col, sid, atype) in cols:
        a = act(f"{PFX}-{sid}", row, col, atype)
        if a:
            activities.append(a)
    itinerary.append({"id": f"{PFX}-d{di+1:02d}", "date": DATES[di],
                      "title": f"Dia {di+1}", "summary": SUMMARIES[di],
                      "activities": activities})

bank_activities = []
for (row, br, cols) in BANK_DEF:
    for (col, sid, atype) in cols:
        a = act(f"{PFX}-{sid}", row, col, atype, bankrow=br)
        if a:
            bank_activities.append(a)

# Dicas
ws_d = wb.sheet_by_name('Dicas')
links, seen_urls, lid, maps_url = [], set(), 1, ""
for r in range(1, ws_d.nrows):
    title = str(ws_d.cell_value(r, 0) or '').strip()
    url   = str(ws_d.cell_value(r, 2) or '').strip() if ws_d.ncols > 2 else ''
    if not title or not url or not url.startswith('http'):
        continue
    if title == 'Mapa':
        maps_url = url
        continue
    if url in seen_urls:
        continue
    seen_urls.add(url)
    links.append({"id": f"{PFX}-l{lid:02d}", "title": title, "url": url})
    lid += 1

# Gastos (BRL)
ws_g = wb.sheet_by_name('Gastos')
expenses, eid = [], 1
TYPE_MAP = {'Passeios e Ingressos': 'Passeios', 'Refeição': 'Refeicao',
            'Hotel': 'Hospedagem', 'Atividades': 'Passeios'}
for r in range(1, ws_g.nrows):
    marker = str(ws_g.cell_value(r, 0) or '').strip()
    if marker not in ('►', '▶', chr(9658), chr(9654)):
        continue
    title  = str(ws_g.cell_value(r, 1) or '').strip()
    etype  = str(ws_g.cell_value(r, 2) or '').strip() or None
    obs1   = str(ws_g.cell_value(r, 3) or '').strip()
    obs2   = str(ws_g.cell_value(r, 4) or '').strip() if ws_g.ncols > 4 else ''
    obs3   = str(ws_g.cell_value(r, 5) or '').strip() if ws_g.ncols > 5 else ''
    pv     = ws_g.cell_value(r, 6)
    price  = float(pv) if isinstance(pv, (int, float)) else 0.0
    pplv   = ws_g.cell_value(r, 8)
    people = int(round(float(pplv))) if isinstance(pplv, (int, float)) else 1
    qv     = ws_g.cell_value(r, 9)
    qty    = int(qv) if isinstance(qv, (int, float)) and qv else 1
    paidv  = ws_g.cell_value(r, 11)
    paid   = float(paidv) if isinstance(paidv, (int, float)) else 0.0

    if not title:
        title = obs1 if obs1 and obs1 != '-' else (obs2 if obs2 and obs2 != '-' else 'Item')

    etype = TYPE_MAP.get(etype, etype)
    if not etype:
        tl = title.lower()
        if any(w in tl for w in ['hotel','pousada','glamping','hostel']): etype = 'Hospedagem'
        elif any(w in tl for w in ['voo','vôo','aluguel','traslado','transfer']): etype = 'Transporte'
        elif any(w in tl for w in ['ingresso','excursao','guia','trilha']): etype = 'Passeios'

    notes = ' . '.join(p for p in [obs1, obs2, obs3] if p and p not in ('-', '')) or None

    e = {"id": f"{PFX}-e{eid:02d}", "isActive": True, "title": title}
    if etype: e["type"] = etype
    if notes: e["notes"] = notes
    e.update({"price": round(price, 2), "taxes": 0.0, "people": people, "quantity": qty,
               "currency": "BRL", "exchangeRateToBase": 1.0, "paidAmount": round(paid, 2)})
    expenses.append(e)
    eid += 1

files = sorted([f for f in os.listdir(FOLDER)
                if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER, f))])
attachments = [{"id": f"{PFX}-att{i+1:02d}", "file": f} for i, f in enumerate(files)]

trip = {
    "schemaVersion": 1, "id": PFX, "title": "2020-05 Jalap" + chr(227) + "o",
    "startDate": "2020-05-12", "endDate": "2020-05-18",
    "baseCurrency": "BRL", "people": PEOPLE, "rateDecimalDigits": 2,
    "myMapsUrl": maps_url, "itinerarySlotsPerDay": SLOTS_PER_DAY,
    "itineraryVersions": [{"id": f"{PFX}-v1", "name": "Versao 1", "bankRows": 1,
                            "itinerary": itinerary, "bankActivities": bank_activities}],
    "activeVersionId": f"{PFX}-v1",
    "tasks": [], "links": links, "expenses": expenses, "currencyRates": [],
    "attachments": attachments
}

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f:
    json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)}d | {sum(len(d['activities']) for d in itinerary)}a | {len(bank_activities)}bk | {len(links)}dicas | {len(expenses)}g | {len(attachments)}att")
