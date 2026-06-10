import openpyxl, json, os, sys
from import_xls_utils import parse_xlsx_roteiro, parse_gastos_xlsx, parse_dicas_xlsx, build_trip
sys.stdout.reconfigure(encoding='utf-8')

FOLDER = r'G:\Meu Drive\Viagens\2017-07 Jericoacoara'
XLSX   = FOLDER + r'\Viagem Jericoacoara.xlsx'
PFX    = 'jericoacoara-2017'

wb      = openpyxl.load_workbook(XLSX)
wb_data = openpyxl.load_workbook(XLSX, data_only=True)

# Col A(0)=date, Col D(3)=summary, activities E-K (0-idx 4-10), hosp_col=K(10)
itinerary = parse_xlsx_roteiro(wb_data['Roteiro'],
    date_col=0, sum_col=3, act_cols=range(4, 11), first_col=4, data_start=3,
    pfx=PFX, hosp_col=10)

links, maps_url = parse_dicas_xlsx(wb_data['Dicas'], PFX, title_col=0, url_col=1)
# Gastos: pi=6(G), ti=7(H), ppli=8(I), qi=9(J), pagi=11(L)
expenses = parse_gastos_xlsx(wb['Gastos'], wb_data['Gastos'], PFX,
    pi=6, ppli=8, qi=9, pagi=11, ti=7, obs_range=(3, 6))

files = sorted([f for f in os.listdir(FOLDER) if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

start = itinerary[0]['date'] if itinerary else ''
end   = itinerary[-1]['date'] if itinerary else ''
trip = build_trip(PFX, "2017-07 Jericoacoara",
    start, end, 3, maps_url, itinerary, expenses, links, attachments)

out = os.path.join(FOLDER, 'trip.json')
with open(out, 'w', encoding='utf-8') as f: json.dump(trip, f, ensure_ascii=False, indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(expenses)} gastos | {len(attachments)} anexos")
