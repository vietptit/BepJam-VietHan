from search_commons import search
for key, query in [('tam10','"làng bích họa Tam Thanh"'), ('tam11','"Tam Thanh" "village" -Melbourne'), ('tam12','"땀타인"'), ('wharf','"Ninh Kieu" river')]:
    try:
        print(search((key,query)))
    except Exception as exc:
        print(exc)
