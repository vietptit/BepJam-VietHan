from search_commons import search
for key, query in [('tam6', 'intitle:"Tam Thanh"'), ('tam7', 'intitle:"Tam Ky"'), ('tam8', 'intitle:"bích họa"'), ('tam9', 'intitle:"Tam Kỳ"')]:
    try:
        print(search((key, query)))
    except Exception as exc:
        print(exc)
