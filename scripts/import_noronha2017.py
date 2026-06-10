import openpyxl, json, os, sys
from import_xls_utils import parse_xlsx_roteiro, parse_gastos_xlsx, parse_dicas_xlsx, build_trip
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2017-10 Fernando de Noronha'
XLSX   = FOLDER + r'\Viagem Fernando de Noronha.xlsx'
PFX    = 'noronha-2017'

wb      = openpyxl.load_workbook(XLSX)
wb_data = openpyxl.load_workbook(XLSX, data_only=True)

# Col A(0)=date, Col C(2)=summary, activities D-H (0-idx 3-7), skip I(8)/J(9)=TAMAR+maré
itinerary = parse_xlsx_roteiro(wb_data['Roteiro'],
    date_col=0, sum_col=2, act_cols=range(3, 10), first_col=3, data_start=3,
    pfx=PFX, skip_cols={8, 9})

links, maps_url = parse_dicas_xlsx(wb_data['Dicas'], PFX, title_col=0, url_col=1)
# Gastos: pi=7(H), ti=8(I), ppli=9(J), qi=10(K), pagi=12(M)
expenses = parse_gastos_xlsx(wb['Gastos'], wb_data['Gastos'], PFX,
    pi=7, ppli=9, qi=10, pagi=12, ti=8, obs_range=(3, 7))

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

start = itinerary[0]['date'] if itinerary else ''
end   = itinerary[-1]['date'] if itinerary else ''
trip = build_trip(PFX, "2017-10 Fernando de Noronha",
    start, end, 2, maps_url, itinerary, expenses, links, attachments)

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f: json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
