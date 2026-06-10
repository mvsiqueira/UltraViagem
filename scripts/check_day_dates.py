"""
Verifica, em todos os trip.json, se startDate bate com a data do primeiro dia
do roteiro ativo. Também reporta qualquer title de dia fora do padrão "Dia N".
"""
import json, os, re, sys
sys.stdout.reconfigure(encoding='utf-8')

BASE = r'G:\Meu Drive\Viagens'

ok = 0
warn = []

for folder in sorted(os.listdir(BASE)):
    path = os.path.join(BASE, folder, 'trip.json')
    if not os.path.isfile(path):
        continue

    with open(path, encoding='utf-8') as f:
        trip = json.load(f)

    trip_id      = trip.get('id', folder)
    start_date   = trip.get('startDate', '')
    active_ver   = trip.get('activeVersionId', '')
    versions     = trip.get('itineraryVersions', [])

    active = next((v for v in versions if v.get('id') == active_ver), versions[0] if versions else None)
    if not active:
        warn.append(f'  ⚠  {trip_id}: sem versão ativa')
        continue

    days = active.get('itinerary', [])
    if not days:
        warn.append(f'  ⚠  {trip_id}: roteiro vazio')
        continue

    first_date = days[0].get('date', '')

    # Verifica startDate x data do dia 1
    if start_date != first_date:
        warn.append(f'  ✗  {trip_id}: startDate={start_date!r}  !=  day[0].date={first_date!r}')
    else:
        ok += 1

    # Verifica datas consecutivas a partir do primeiro dia
    for i, day in enumerate(days):
        d = day.get('date', '')
        if not d:
            warn.append(f'  ⚠  {trip_id} dia {i+1}: sem campo date')
        # checar consecutividade seria mais trabalhoso; avisamos só ausência

    # Verifica titles fora do padrão "Dia N"
    for i, day in enumerate(days):
        title = day.get('title', '')
        if title and not re.fullmatch(r'Dia \d+', title):
            warn.append(f'  ⚠  {trip_id} dia {i+1}: title customizado = {title!r}')

print(f'Verificados: {ok + len(warn)} arquivos  |  OK: {ok}  |  Alertas: {len(warn)}')
if warn:
    print()
    for w in warn:
        print(w)
else:
    print('Tudo certo — pode rodar o cleanup com segurança.')
