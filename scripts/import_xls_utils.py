"""Utilitários para importação de arquivos XLS (xlrd) e XLSX (openpyxl) — viagens 2015-2017."""
import datetime, re

TYPE_COLORS = {
    'Transporte': '#4F81BD', 'Refeicao':   '#F79646',
    'Hospedagem': '#9BBB59', 'Passeio':    '#4BACC6',
}

def xls_date(wb, serial):
    return datetime.datetime(*__import__('xlrd').xldate_as_tuple(serial, wb.datemode)[:3])

def detect_type(text, col=None, hosp_col=None):
    if hosp_col is not None and col == hosp_col:
        return 'Hospedagem'
    tl = text.lower()
    if any(w in tl for w in ['v\xf4o','voo ','traslado','transfer',
                              'aluguel','gasolina','carro x ','rodovi']):
        return 'Transporte'
    if any(w in tl for w in ['hotel','pousada','hostel','resort','zagaia','inn']):
        return 'Hospedagem'
    if any(w in tl for w in ['almo\xe7o','jantar','restaurante','refei',
                              'buffet','pizza','lanche','churrasco','bar do']):
        return 'Refeicao'
    return 'Passeio'

def parse_xls_roteiro(wb, ws, date_col, sum_col, act_cols, first_col, data_start, pfx,
                      hosp_col=None, skip_cols=None):
    """Gera itinerary a partir de um Roteiro XLS.
    - Itera rows a partir de data_start (0-indexed) enquanto date_col for float.
    - act_cols: range ou lista de colunas de atividade (0-indexed).
    - hosp_col: coluna que é sempre Hospedagem (ex: pernoite).
    - skip_cols: set de colunas a ignorar completamente.
    """
    skip_cols = set(skip_cols or [])
    itinerary = []
    aid = 1
    di  = 0
    for row in range(data_start, ws.nrows):
        date_v = ws.cell(row, date_col).value
        if not isinstance(date_v, float):
            break
        date_str = xls_date(wb, date_v).strftime('%Y-%m-%d')
        summary  = str(ws.cell(row, sum_col).value or '').strip()
        if not summary:
            summary = f'Dia {di + 1}'
        activities = []
        for col in act_cols:
            if col in skip_cols or col >= ws.ncols:
                continue
            raw = ws.cell(row, col).value
            if raw is None or str(raw).strip() in ('', '-'):
                continue
            text   = str(raw).strip()
            parts  = text.replace('\n', '|').split('|')
            title  = parts[0].strip()
            detail = ' \xb7 '.join(p.strip() for p in parts[1:] if p.strip()) or None
            if not title:
                continue
            atype  = detect_type(text, col, hosp_col)
            a = {
                'id': f'{pfx}-a{aid:02d}',
                'title': title, 'type': atype,
                'color': TYPE_COLORS.get(atype, '#4BACC6'),
                'icon': '',
                'startSlot': col - first_col,
                'durationSlots': 1,
                'bankRow': 0,
            }
            if detail:
                a['details'] = detail
            activities.append(a)
            aid += 1
        itinerary.append({
            'id': f'{pfx}-d{di + 1:02d}',
            'date': date_str,
            'title': f'Dia {di + 1}',
            'summary': summary,
            'activities': activities,
        })
        di += 1
    return itinerary

def parse_gastos_xls(ws_g, pfx, pi, ppli, qi, pagi,
                     ti=None, moedai=None, obs_range=(3, 7)):
    """Parser de gastos XLS.
    - Marcador (►/▶/>) deve estar em col 0.
    - moedai: coluna da taxa de câmbio; se None, preço fica em BRL direto.
    - obs_range: intervalo (start, end) de colunas de observação (exclusive end).
    """
    expenses = []; eid = 1
    for r in range(1, ws_g.nrows):
        marker = str(ws_g.cell(r, 0).value or '').strip()
        if marker not in ('>', '►', '▶'):
            continue
        title  = str(ws_g.cell(r, 1).value or '').strip()
        etype  = str(ws_g.cell(r, 2).value or '').strip() or None
        obs_parts = [
            str(ws_g.cell(r, i).value or '').strip()
            for i in range(*obs_range)
            if ws_g.ncols > i
            and ws_g.cell(r, i).value
            and str(ws_g.cell(r, i).value).strip() not in ('', '-')
        ]
        price_v    = ws_g.cell(r, pi).value   if ws_g.ncols > pi    else None
        taxes_v    = ws_g.cell(r, ti).value   if ti    is not None and ws_g.ncols > ti    else None
        exchange_v = ws_g.cell(r, moedai).value if moedai is not None and ws_g.ncols > moedai else None
        people_v   = ws_g.cell(r, ppli).value if ws_g.ncols > ppli  else None
        qty_v      = ws_g.cell(r, qi).value   if ws_g.ncols > qi    else None
        paid_v     = ws_g.cell(r, pagi).value if ws_g.ncols > pagi  else None

        price    = float(price_v)    if isinstance(price_v,    (int, float)) else 0.0
        taxes    = float(taxes_v)    if isinstance(taxes_v,    (int, float)) else 0.0
        exchange = float(exchange_v) if isinstance(exchange_v, (int, float)) and exchange_v != 0 else 1.0
        if moedai is not None:
            price = round(price * exchange, 2)
            taxes = round(taxes * exchange, 2)
        people = int(round(float(people_v))) if isinstance(people_v, (int, float)) else 1
        qty    = int(qty_v)   if isinstance(qty_v,   (int, float)) else 1
        paid   = float(paid_v) if isinstance(paid_v, (int, float)) else 0.0

        if not title:
            title = obs_parts[0] if obs_parts else 'Item'
        notes = ' . '.join(obs_parts) or None

        if etype == 'Hotel':                             etype = 'Hospedagem'
        if etype in ('Atividades','Passeios e Ingressos',
                     'Ingressos','Passeios e ingr'):     etype = 'Passeios'
        if etype in ('Refei\xe7\xe3o', 'Refei??o'):     etype = 'Refeicao'
        if not etype:
            tl = title.lower()
            if any(w in tl for w in ['hotel','pousada','resort','zagaia']):
                etype = 'Hospedagem'
            elif any(w in tl for w in ['v\xf4o','voo','traslado','transfer',
                                        'aluguel','gasolina','carro']):
                etype = 'Transporte'
            elif any(w in tl for w in ['ingresso','parque','passeio',
                                        'tour','mergulho','excurs']):
                etype = 'Passeios'
            elif any(w in tl for w in ['almo\xe7o','jantar','restaurante',
                                        'refei','buffet','pizza','lanche']):
                etype = 'Refeicao'

        e = {'id': f'{pfx}-e{eid:02d}', 'isActive': True, 'title': title}
        if etype: e['type'] = etype
        if notes: e['notes'] = notes
        e.update({
            'price': round(price, 2), 'taxes': round(taxes, 2),
            'people': people, 'quantity': qty,
            'currency': 'BRL', 'exchangeRateToBase': 1.0,
            'paidAmount': round(paid, 2),
        })
        expenses.append(e); eid += 1
    return expenses

def parse_dicas_xls(ws_d, pfx, title_col=0, url_col=2, start_row=1):
    """Extrai links de um sheet Dicas/Links XLS.
    - Filtra apenas linhas cujo url_col começa com 'http'.
    - Linha com title='Mapa' é extraída como maps_url.
    """
    links = []; seen = set(); lid = 1; maps_url = ''
    for r in range(start_row, ws_d.nrows):
        tv = ws_d.cell(r, title_col).value if ws_d.ncols > title_col else None
        uv = ws_d.cell(r, url_col).value   if ws_d.ncols > url_col   else None
        if not tv or not uv:
            continue
        title = str(tv).strip()
        url   = re.sub(r'\+[A-Z]\d+$', '', str(uv).strip())
        if not url.startswith('http'):
            continue
        if title == 'Mapa':
            maps_url = url; continue
        if url in seen:
            continue
        seen.add(url)
        links.append({'id': f'{pfx}-l{lid:02d}', 'title': title, 'url': url})
        lid += 1
    return links, maps_url

def parse_xlsx_roteiro(ws, date_col, sum_col, act_cols, first_col, data_start, pfx,
                       hosp_col=None, skip_cols=None):
    """Gera itinerary a partir de Roteiro XLSX (openpyxl, data_only ws).
    COLUNAS: 0-indexed (internamente +1 para openpyxl). data_start: linha openpyxl 1-based.
    """
    skip_cols = set(skip_cols or [])
    itinerary = []
    aid = 1; di = 0
    for row in range(data_start, ws.max_row + 1):
        date_v = ws.cell(row, date_col + 1).value
        if date_v is None:
            break
        if not isinstance(date_v, (datetime.datetime, datetime.date)):
            break
        date_str = date_v.strftime('%Y-%m-%d')
        summary  = str(ws.cell(row, sum_col + 1).value or '').strip()
        if not summary:
            summary = f'Dia {di + 1}'
        activities = []
        for col in act_cols:
            if col in skip_cols:
                continue
            raw = ws.cell(row, col + 1).value
            if raw is None or str(raw).strip() in ('', '-'):
                continue
            text   = str(raw).strip()
            parts  = text.replace('\n', '|').split('|')
            title  = parts[0].strip()
            detail = ' \xb7 '.join(p.strip() for p in parts[1:] if p.strip()) or None
            if not title:
                continue
            atype  = detect_type(text, col, hosp_col)
            a = {
                'id': f'{pfx}-a{aid:02d}',
                'title': title, 'type': atype,
                'color': TYPE_COLORS.get(atype, '#4BACC6'),
                'icon': '',
                'startSlot': col - first_col,
                'durationSlots': 1,
                'bankRow': 0,
            }
            if detail:
                a['details'] = detail
            activities.append(a)
            aid += 1
        itinerary.append({
            'id': f'{pfx}-d{di + 1:02d}',
            'date': date_str,
            'title': f'Dia {di + 1}',
            'summary': summary,
            'activities': activities,
        })
        di += 1
    return itinerary


def parse_gastos_xlsx(ws_g, ws_g_data, pfx, pi, ppli, qi, pagi,
                      ti=None, moedai=None, obs_range=(3, 6)):
    """Parser de gastos XLSX (openpyxl). COLUNAS: 0-indexed via row tuple.
    ws_g: workbook normal; ws_g_data: workbook data_only. Marcador em col 0 (A).
    """
    MARKERS = ('>', '►', '▶', chr(9658), chr(9654))
    expenses = []; eid = 1
    for rr, rd in zip(ws_g.iter_rows(min_row=2), ws_g_data.iter_rows(min_row=2)):
        raw = list(rr); dat = list(rd)
        if str(raw[0].value or '').strip() not in MARKERS:
            continue
        title = str(raw[1].value or '').strip()
        etype = str(raw[2].value or '').strip() or None
        obs_parts = [
            str(raw[i].value or '').strip()
            for i in range(*obs_range)
            if len(raw) > i and raw[i].value
            and str(raw[i].value).strip() not in ('', '-')
        ]
        price_r    = raw[pi].value    if len(raw) > pi    else None
        price_d    = dat[pi].value    if len(dat) > pi    else None
        taxes_r    = raw[ti].value    if ti    is not None and len(raw) > ti    else None
        exchange_d = dat[moedai].value if moedai is not None and len(dat) > moedai else None
        ppl_r      = raw[ppli].value  if len(raw) > ppli  else None
        ppl_d      = dat[ppli].value  if len(dat) > ppli  else None
        qty_r      = raw[qi].value    if len(raw) > qi    else None
        paid_d     = dat[pagi].value  if len(dat) > pagi  else None
        paid_r     = raw[pagi].value  if len(raw) > pagi  else None

        price    = float(price_r) if isinstance(price_r,(int,float)) else (float(price_d) if isinstance(price_d,(int,float)) else 0.0)
        taxes    = float(taxes_r) if isinstance(taxes_r,(int,float)) else 0.0
        exchange = float(exchange_d) if isinstance(exchange_d,(int,float)) and exchange_d != 0 else 1.0
        if moedai is not None:
            price = round(price * exchange, 2)
            taxes = round(taxes * exchange, 2)
        people = int(round(float(ppl_r))) if isinstance(ppl_r,(int,float)) else (int(round(float(ppl_d))) if isinstance(ppl_d,(int,float)) else 1)
        qty    = int(qty_r or 1) if qty_r else 1
        paid   = float(paid_d) if isinstance(paid_d,(int,float)) else (float(paid_r) if isinstance(paid_r,(int,float)) else 0.0)

        if not title:
            title = obs_parts[0] if obs_parts else 'Item'
        notes = ' . '.join(obs_parts) or None

        if etype == 'Hotel':                             etype = 'Hospedagem'
        if etype in ('Atividades','Passeios e Ingressos',
                     'Ingressos','Passeios e ingr'):     etype = 'Passeios'
        if etype in ('Refei\xe7\xe3o',):                etype = 'Refeicao'
        if not etype:
            tl = title.lower()
            if any(w in tl for w in ['hotel','pousada','resort']): etype = 'Hospedagem'
            elif any(w in tl for w in ['v\xf4o','voo','traslado','transfer']): etype = 'Transporte'
            elif any(w in tl for w in ['almo\xe7o','jantar','restaurante']): etype = 'Refeicao'

        e = {'id': f'{pfx}-e{eid:02d}', 'isActive': True, 'title': title}
        if etype: e['type'] = etype
        if notes: e['notes'] = notes
        e.update({'price': round(price,2), 'taxes': round(taxes,2), 'people': people,
                  'quantity': qty, 'currency': 'BRL', 'exchangeRateToBase': 1.0,
                  'paidAmount': round(paid,2)})
        expenses.append(e); eid += 1
    return expenses


def parse_dicas_xlsx(ws_d, pfx, title_col=0, url_col=1, start_row=2):
    """Extrai links de Dicas XLSX (openpyxl). title_col, url_col: 0-indexed via row tuple."""
    links = []; seen = set(); lid = 1; maps_url = ''
    for row in ws_d.iter_rows(min_row=start_row):
        tv = row[title_col].value if len(row) > title_col else None
        uv = row[url_col].value   if len(row) > url_col   else None
        if not tv or not uv:
            continue
        title = str(tv).strip()
        url   = re.sub(r'\+[A-Z]\d+$', '', str(uv).strip())
        if not url.startswith('http'):
            continue
        if title == 'Mapa':
            maps_url = url; continue
        if url in seen:
            continue
        seen.add(url)
        links.append({'id': f'{pfx}-l{lid:02d}', 'title': title, 'url': url})
        lid += 1
    return links, maps_url


def build_trip(pfx, title, start, end, people, maps_url, itinerary,
               expenses, links, attachments, currency='BRL'):
    return {
        'schemaVersion': 1, 'id': pfx, 'title': title,
        'startDate': start, 'endDate': end,
        'baseCurrency': currency, 'people': people,
        'rateDecimalDigits': 2, 'myMapsUrl': maps_url,
        'itinerarySlotsPerDay': 7,
        'itineraryVersions': [{
            'id': f'{pfx}-v1',
            'name': 'Vers\xe3o 1',
            'bankRows': 3,
            'itinerary': itinerary,
            'bankActivities': [],
        }],
        'activeVersionId': f'{pfx}-v1',
        'tasks': [], 'links': links, 'expenses': expenses,
        'currencyRates': [], 'attachments': attachments,
    }
