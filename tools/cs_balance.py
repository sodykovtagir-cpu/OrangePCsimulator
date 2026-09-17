import sys

def balance(path):
    s = open(path, encoding='utf-8').read()
    i = 0; n = len(s)
    depth = 0; paren = 0
    state = None  # None | 'line' | 'block' | 'str' | 'char' | 'verb'
    while i < n:
        c = s[i]; nxt = s[i+1] if i+1 < n else ''
        if state is None:
            if c == '/' and nxt == '/': state='line'; i+=2; continue
            if c == '/' and nxt == '*': state='block'; i+=2; continue
            if c == '@' and nxt == '"': state='verb'; i+=2; continue
            if c == '"': state='str'; i+=1; continue
            if c == "'": state='char'; i+=1; continue
            if c == '{': depth+=1
            elif c == '}': depth-=1
            elif c == '(': paren+=1
            elif c == ')': paren-=1
            i+=1; continue
        if state=='line':
            if c=='\n': state=None
            i+=1; continue
        if state=='block':
            if c=='*' and nxt=='/': state=None; i+=2; continue
            i+=1; continue
        if state=='str':
            if c=='\\': i+=2; continue
            if c=='"': state=None
            i+=1; continue
        if state=='verb':
            if c=='"' and nxt=='"': i+=2; continue
            if c=='"': state=None
            i+=1; continue
        if state=='char':
            if c=='\\': i+=2; continue
            if c=="'": state=None
            i+=1; continue
    return depth, paren

for p in sys.argv[1:]:
    d, pr = balance(p)
    flag = "OK " if (d==0 and pr==0) else "BAD"
    print(f"{flag} {{}}={d:+d} ()={pr:+d}  {p}")
