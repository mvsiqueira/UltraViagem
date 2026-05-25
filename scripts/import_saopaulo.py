import openpyxl, json, os, re, sys
from import_utils import build_merge_map, make_activity_fn
sys.stdout.reconfigure(encoding='utf-8')

XLSX   = r'G:\Meu Drive\Viagens\2023-09 São Paulo\Viagem São Paulo.xlsx'
FOLDER = r'G:\Meu Drive\Viagens\2023-09 São Paulo'
PFX    = 'saopaulo-2023'

wb = openpyxl.load_workbook(XLSX)
ws = wb['Roteiro']
merge_map = build_merge_map(ws)
act = make_activity_fn(ws, merge_map, first_col=4)

DATES     = ["2023-09-07","2023-09-08","2023-09-09","2023-09-10","2023-09-11"]
SUMMARIES = ["IDA","São Paulo 1","São Paulo 2","The Town","VOLTA"]

DAYS_DEF = [
    (3, 0, [(4,'a01','Transporte'),(11,'a02','Hospedagem')]),
    (4, 1, [(6,'a03','Refeição'),(11,'a04','Hospedagem')]),
    (5, 2, [(4,'a05','Passeio'),(6,'a06','Refeição'),(11,'a07','Hospedagem')]),
    (6, 3, [(4,'a08','Passeio'),(6,'a09','Refeição'),(7,'a10','Passeio'),(11,'a11','Hospedagem')]),
    (7, 4, [(4,'a12','Transporte')]),
]

itinerary = []
for (row, di, cols) in DAYS_DEF:
    activities = []
    for (col, sid, atype) in cols:
        mc = merge_map.get((row, col))
        if mc and mc.min_col != col: continue
        a = act(f"{PFX}-{sid}", row, col, atype)
        if a: activities.append(a)
    itinerary.append({"id":f"{PFX}-d{di+1:02d}","date":DATES[di],
                      "title":f"Dia {di+1}","summary":SUMMARIES[di],"activities":activities})

ws_d = wb['Dicas']
links = []

files = sorted([f for f in os.listdir(FOLDER)
                if not f.startswith('.') and os.path.isfile(os.path.join(FOLDER,f))])
attachments = [{"id":f"{PFX}-att{i+1:02d}","file":f} for i,f in enumerate(files)]

trip = {
    "schemaVersion":1,"id":PFX,"title":"2023-09 São Paulo",
    "startDate":"2023-09-07","endDate":"2023-09-11",
    "baseCurrency":"BRL","people":2,"rateDecimalDigits":2,"myMapsUrl":"",
    "itinerarySlotsPerDay":8,
    "itineraryVersions":[{"id":f"{PFX}-v1","name":"Versão 1","bankRows":0,
                          "itinerary":itinerary,"bankActivities":[]}],
    "activeVersionId":f"{PFX}-v1",
    "tasks":[],"links":links,"expenses":[],"currencyRates":[],"attachments":attachments
}

out = os.path.join(FOLDER,'trip.json')
with open(out,'w',encoding='utf-8') as f: json.dump(trip,f,ensure_ascii=False,indent=2)
print(f"OK: {out}")
print(f"  {len(itinerary)} dias | {sum(len(d['activities']) for d in itinerary)} ativ | {len(links)} dicas | {len(attachments)} anexos")
