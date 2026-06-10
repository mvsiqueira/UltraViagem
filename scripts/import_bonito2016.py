import xlrd, json, os, sys
from import_xls_utils import parse_xls_roteiro, parse_gastos_xls, parse_dicas_xls, build_trip
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2016-08 Bonito'
XLS    = FOLDER + r'\Viagem Bonito.xls'
PFX    = 'bonito-2016'

wb = xlrd.open_workbook(XLS)
itinerary = parse_xls_roteiro(wb, wb.sheet_by_name('Roteiro'),
    date_col=0, sum_col=2, act_cols=range(3, 10), first_col=3, data_start=2, pfx=PFX)

links, maps_url = parse_dicas_xls(wb.sheet_by_name('Dicas'), PFX, title_col=0, url_col=2)
expenses = parse_gastos_xls(wb.sheet_by_name('Gastos'), PFX, pi=7, ppli=8, qi=9, pagi=11, obs_range=(3,7))

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

start = itinerary[0]['date'] if itinerary else ''
end   = itinerary[-1]['date'] if itinerary else ''
trip = build_trip(PFX, "2016-08 Bonito",
    start, end, 4, maps_url, itinerary, expenses, links, attachments)

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f: json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
