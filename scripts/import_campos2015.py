import xlrd, json, os, sys
from import_xls_utils import parse_xls_roteiro, parse_dicas_xls, build_trip
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2015-04 Campos do Jord' + chr(227) + 'o'
XLS    = FOLDER + r'\Viagem Campos do Jord' + chr(227) + 'o.xls'
PFX    = 'campos-2015'

wb = xlrd.open_workbook(XLS)
# Formato antigo: linha 0=vazio, 1=título, 2=labels, 3+=dados; date_col=1, sum_col=4
itinerary = parse_xls_roteiro(wb, wb.sheet_by_name('Roteiro'),
    date_col=1, sum_col=4, act_cols=range(5, 10), first_col=5, data_start=3, pfx=PFX)

links, maps_url = parse_dicas_xls(wb.sheet_by_name('Links'), PFX, title_col=1, url_col=2)

# Gastos especial: marcador em col 2, título col 3, preço col 4, pago col 5
def _parse_gastos_campos(ws_g, pfx):
    MARKERS = ('>', '►', '▶', chr(9658), chr(9654))
    expenses = []; eid = 1
    for r in range(ws_g.nrows):
        marker = str(ws_g.cell(r, 2).value or '').strip()
        if marker not in MARKERS:
            continue
        title   = str(ws_g.cell(r, 3).value or '').strip()
        price_v = ws_g.cell(r, 4).value
        paid_v  = ws_g.cell(r, 5).value if ws_g.ncols > 5 else None
        if not title:
            continue
        price = float(price_v) if isinstance(price_v, (int, float)) else 0.0
        paid  = float(paid_v)  if isinstance(paid_v,  (int, float)) else 0.0
        e = {'id': f'{pfx}-e{eid:02d}', 'isActive': True, 'title': title,
             'price': round(price, 2), 'taxes': 0.0, 'people': 1, 'quantity': 1,
             'currency': 'BRL', 'exchangeRateToBase': 1.0, 'paidAmount': round(paid, 2)}
        expenses.append(e); eid += 1
    return expenses

expenses = _parse_gastos_campos(wb.sheet_by_name('Gastos'), PFX)

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

start = itinerary[0]['date'] if itinerary else ''
end   = itinerary[-1]['date'] if itinerary else ''
trip = build_trip(PFX, "2015-04 Campos do Jord" + chr(227) + "o",
    start, end, 4, maps_url, itinerary, expenses, links, attachments)

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f: json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
