import openpyxl, json, os, sys
from import_xls_utils import parse_xlsx_roteiro, parse_dicas_xlsx, build_trip
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2017-01 Penedo'
XLSX   = FOLDER + r'\Viagem Penedo.xlsx'
PFX    = 'penedo-2017'

wb      = openpyxl.load_workbook(XLSX)
wb_data = openpyxl.load_workbook(XLSX, data_only=True)

# Col B(1)=date, Col D(3)=summary, activities E-I (0-idx 4-8), first_col=4
itinerary = parse_xlsx_roteiro(wb_data['Roteiro'],
    date_col=1, sum_col=3, act_cols=range(4, 9), first_col=4, data_start=3, pfx=PFX)

links, maps_url = parse_dicas_xlsx(wb_data['Dicas'], PFX, title_col=0, url_col=1)
expenses = []  # Gastos vazio

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

start = itinerary[0]['date'] if itinerary else ''
end   = itinerary[-1]['date'] if itinerary else ''
trip = build_trip(PFX, "2017-01 Penedo",
    start, end, 2, maps_url, itinerary, expenses, links, attachments)

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f: json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
