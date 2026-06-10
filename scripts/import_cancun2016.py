import xlrd, json, os, sys
from import_xls_utils import parse_xls_roteiro, parse_gastos_xls, parse_dicas_xls, build_trip
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2016-05 Canc' + chr(250) + 'n'
XLS    = FOLDER + r'\Viagem Cancun.xls'
PFX    = 'cancun-2016'

wb = xlrd.open_workbook(XLS)
# Cancún tem 11 colunas de atividade (cols 3-10, incluindo cols intermediárias vazias)
itinerary = parse_xls_roteiro(wb, wb.sheet_by_name('Roteiro'),
    date_col=0, sum_col=2, act_cols=range(3, 11), first_col=3, data_start=2, pfx=PFX)

links, maps_url = parse_dicas_xls(wb.sheet_by_name('Dicas'), PFX, title_col=0, url_col=2)
# Gastos 14-col com câmbio: pi=8, moedai=9, ppli=10, qi=11, pagi=13
expenses = parse_gastos_xls(wb.sheet_by_name('Gastos'), PFX,
    pi=8, ppli=10, qi=11, pagi=13, moedai=9, obs_range=(3, 8))

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

trip = build_trip(PFX, "2016-05 Canc" + chr(250) + "n",
    "2016-05-09", "2016-05-20", 2, maps_url, itinerary, expenses, links, attachments)

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f: json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
