import xlrd, json, os, sys
from import_xls_utils import parse_xls_roteiro, parse_gastos_xls, parse_dicas_xls, build_trip
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2015-10 S' + chr(227) + 'o Miguel dos Milagres'
XLS    = FOLDER + r'\Viagem S' + chr(227) + 'o Miguel dos Milagres II.xls'
PFX    = 'saomiguel-2015-out'

wb = xlrd.open_workbook(XLS)
# col 3 = marés (tide info) → skip; activities em cols 4-8
itinerary = parse_xls_roteiro(wb, wb.sheet_by_name('Roteiro'),
    date_col=0, sum_col=2, act_cols=range(3, 9), first_col=3, data_start=2, pfx=PFX,
    skip_cols={3})

links, maps_url = parse_dicas_xls(wb.sheet_by_name('Dicas'), PFX, title_col=0, url_col=2)
expenses = parse_gastos_xls(wb.sheet_by_name('Gastos'), PFX, pi=7, ppli=8, qi=9, pagi=11)

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

trip = build_trip(PFX, "2015-10 S" + chr(227) + "o Miguel dos Milagres",
    "2015-10-12", "2015-10-17", 5, maps_url, itinerary, expenses, links, attachments)

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f: json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
