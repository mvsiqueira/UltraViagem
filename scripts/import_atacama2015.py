import xlrd, json, os, sys
from import_xls_utils import parse_xls_roteiro, parse_dicas_xls, build_trip
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2015-08 Atacama'
XLS    = FOLDER + r'\Viagem Atacama.xls'
PFX    = 'atacama-2015'

wb = xlrd.open_workbook(XLS)
# Formato antigo: linha 0=vazio, 1=título, 2=labels, 3+=dados; date_col=1, sum_col=3
# Col 10 = fração da lua (info extra) → skip_cols={10}
itinerary = parse_xls_roteiro(wb, wb.sheet_by_name('Roteiro'),
    date_col=1, sum_col=3, act_cols=range(4, 11), first_col=4, data_start=3,
    pfx=PFX, skip_cols={10})

# Dicas = lista de o que levar (sem URLs) → links vazio
links, maps_url = parse_dicas_xls(wb.sheet_by_name('Dicas'), PFX, title_col=0, url_col=1)
expenses = []

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

start = itinerary[0]['date'] if itinerary else ''
end   = itinerary[-1]['date'] if itinerary else ''
trip = build_trip(PFX, "2015-08 Atacama",
    start, end, 2, maps_url, itinerary, expenses, links, attachments)

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f: json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
