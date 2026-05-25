"""Utilitários compartilhados pelos scripts de importação."""
import colorsys

THEME = {0:'000000',1:'FFFFFF',2:'1F497D',3:'EEECE1',
         4:'4F81BD',5:'C0504D',6:'9BBB59',7:'8064A2',
         8:'4BACC6',9:'F79646',10:'0000FF',11:'800080'}

def apply_tint(h6, t):
    r,g,b = int(h6[0:2],16)/255,int(h6[2:4],16)/255,int(h6[4:6],16)/255
    hh,l,s = colorsys.rgb_to_hls(r,g,b)
    l = l*(1-t)+t if t>0 else l*(1+t)
    l = max(0.0,min(1.0,l))
    r2,g2,b2 = colorsys.hls_to_rgb(hh,l,s)
    return f"#{round(r2*255):02X}{round(g2*255):02X}{round(b2*255):02X}"

def cell_color(cell):
    try:
        fill_type = cell.fill.fill_type
        if fill_type != 'solid': return '#000000'
        fg = cell.fill.fgColor
    except AttributeError:
        return '#000000'
    if fg.type == 'rgb':
        rgb = fg.rgb[-6:].upper()
        if rgb == '000000' and fg.rgb[:2] == '00': return '#000000'
        return f"#{rgb}"
    if fg.type == 'theme':
        b = THEME.get(fg.theme,'000000')
        return apply_tint(b,fg.tint) if fg.tint != 0 else f"#{b.upper()}"
    return '#000000'

def parse_text(value):
    """Retorna (title, details) normalizando \\n e | como separadores."""
    text = str(value or '').strip()
    if not text or text == '-': return None, None
    # Normaliza \n → | para tratar ambos igualmente
    normalized = text.replace('\n', '|')
    if '|' in normalized:
        parts = [p.strip() for p in normalized.split('|') if p.strip()]
        return parts[0], ' · '.join(parts[1:]) if len(parts) > 1 else ''
    return text, ''

def build_merge_map(ws):
    m = {}
    for mc in ws.merged_cells.ranges:
        for r in range(mc.min_row, mc.max_row+1):
            for c in range(mc.min_col, mc.max_col+1):
                m[(r,c)] = mc
    return m

def make_activity_fn(ws, merge_map, first_col):
    def make_activity(aid, row, col, atype, bankrow=0):
        mc = merge_map.get((row, col))
        if mc:
            min_c, max_c = mc.min_col, mc.max_col
            real_cell = ws.cell(row=mc.min_row, column=mc.min_col)
        else:
            min_c = max_c = col
            real_cell = ws.cell(row=row, column=col)
        title, details = parse_text(real_cell.value)
        if not title: return None
        color = cell_color(real_cell)
        a = {"id": aid, "title": title, "type": atype, "color": color, "icon": "",
             "startSlot": min_c - first_col, "durationSlots": max_c - min_c + 1, "bankRow": bankrow}
        if details: a["details"] = details
        return a
    return make_activity

def parse_expenses(ws_g, ws_g_data, id_prefix, moeda_keys=None):
    """
    moeda_keys: dict com chaves a procurar na fórmula da coluna Moeda.
    Padrão: {'Peso':'CLP','peso':'CLP','Dólar':'USD','Dolar':'USD','CLP':'CLP','USD':'USD'}
    """
    if moeda_keys is None:
        moeda_keys = {'Peso':'CLP','peso':'CLP','Dólar':'USD','Dolar':'USD',
                      'CLP':'CLP','USD':'USD'}

    expenses = []
    eid = 1
    rows_raw  = list(ws_g.iter_rows(min_row=2))
    rows_data = list(ws_g_data.iter_rows(min_row=2))

    for row_raw, row_dat in zip(rows_raw, rows_data):
        raw = list(row_raw); dat = list(row_dat)
        marker = str(raw[0].value or '').strip()
        if marker not in ('►','▶'): continue

        title   = str(raw[1].value or '').strip()
        etype   = str(raw[2].value or '').strip() or None
        company = str(raw[3].value or '').strip()
        obs1    = str(raw[5].value or '').strip()
        obs2    = str(raw[6].value or '').strip()
        obs3    = str(raw[7].value or '').strip()

        price_raw = raw[8].value; price_dat = dat[8].value
        if isinstance(price_raw,(int,float)): price = float(price_raw)
        elif isinstance(price_dat,(int,float)): price = float(price_dat)
        else: price = 0.0

        taxes_raw = raw[9].value
        taxes = float(taxes_raw) if isinstance(taxes_raw,(int,float)) else 0.0

        people = int(raw[10].value or 1) if raw[10].value else 1
        qty    = int(raw[11].value or 1) if raw[11].value else 1

        moeda_raw  = str(raw[13].value or '1').strip()
        moeda_calc = dat[13].value
        currency, rate = 'BRL', 1.0
        for key, cur in moeda_keys.items():
            if key in moeda_raw:
                currency = cur
                rate = float(moeda_calc) if isinstance(moeda_calc,(int,float)) else 0.0
                break

        paid_raw  = raw[15].value
        paid_calc = dat[15].value
        if isinstance(paid_calc,(int,float)): paid = float(paid_calc)
        elif isinstance(paid_raw,str) and paid_raw.startswith('='): paid = (price+taxes)*people*qty*rate
        else: paid = 0.0

        if not title:
            title = f"Transfer ({obs1})" if obs1 and obs1 != '-' else \
                    (company if company and company != '-' else 'Item')

        note_parts = [p for p in [obs1,obs2,obs3] if p and p not in ('-','')]
        notes = ' · '.join(note_parts) if note_parts else None
        company = company if company and company != '-' else None

        if not etype:
            tl = title.lower()
            if any(w in tl for w in ['hotel','pousada','glamping','hostal','hostel','cabaña','cabana','apart']): etype = 'Hospedagem'
            elif any(w in tl for w in ['camping']): etype = 'Hospedagem'
            elif any(w in tl for w in ['traslado','transfer','vôo','voo','voo ','aluguel','motorhome','gasolina']): etype = 'Transporte'
            elif any(w in tl for w in ['ingresso','excursão','excursao','rafting']): etype = 'Passeios'
        if etype in ('Hotel','Atividades'): etype = 'Hospedagem' if etype=='Hotel' else 'Passeios'

        e = {"id":f"{id_prefix}-e{eid:02d}","isActive":True,"title":title}
        if etype:   e["type"]    = etype
        if company: e["company"] = company
        if notes:   e["notes"]   = notes
        e["price"]              = round(price,2)
        e["taxes"]              = round(taxes,2)
        e["people"]             = people
        e["quantity"]           = qty
        e["currency"]           = currency
        e["exchangeRateToBase"] = round(rate,6)
        e["paidAmount"]         = round(paid,2)
        expenses.append(e)
        eid += 1

    return expenses
