# Usage: python3 verify_solver.py  (no dependencies; ~2 s)
# Independent solver for Tic-Tac-Fade (3x3, buffer 3, win length 3), written from GDD rules only.
from collections import deque, defaultdict
import time

N = 3; BUF = 3
LINES = [(0,1,2),(3,4,5),(6,7,8),(0,3,6),(1,4,7),(2,5,8),(0,4,8),(2,4,6)]

def has_line(cells):
    s = set(cells)
    return any(a in s and b in s and c in s for a,b,c in LINES)

# state: (qX, qO, turn) ; turn 0 = X to move, 1 = O to move ; queues oldest first
def moves(state):
    qx, qo, t = state
    occupied = set(qx) | set(qo)   # cell must be free BEFORE the move (replacement restriction)
    mine = qx if t == 0 else qo
    out = []
    for c in range(9):
        if c in occupied: continue
        nq = list(mine) + [c]
        if len(nq) > BUF: nq = nq[1:]          # FIFO after placing
        nq = tuple(nq)
        win = has_line(nq)                      # win evaluated after FIFO
        if t == 0: ns = (nq, qo, 1)
        else:      ns = (qx, nq, 0)
        out.append((c, win, ns))
    return out

t0 = time.time()
start = ((), (), 0)
succ = {}
q = deque([start]); seen = {start}
edges = 0
while q:
    s = q.popleft()
    ms = moves(s)
    succ[s] = ms
    edges += len(ms)
    for c, win, ns in ms:
        if not win and ns not in seen:
            seen.add(ns); q.append(ns)
print("positions", len(succ), "edges", edges, "explore_s", round(time.time()-t0,2))

# retrograde
pred = defaultdict(list)
remaining = {}
val = {}   # s -> (outcome, dist)  outcome 'W'/'L'
for s, ms in succ.items():
    remaining[s] = sum(1 for _,w,_ in ms if not w)
    for c, w, ns in ms:
        if not w: pred[ns].append(s)
queue = deque()
for s, ms in succ.items():
    if any(w for _,w,_ in ms):
        val[s] = ('W', 1); queue.append(s)
# process in increasing distance: queue is FIFO and distances assigned nondecreasing
maxchild = defaultdict(int)
while queue:
    s = queue.popleft()
    o, d = val[s]
    for p in pred[s]:
        if p in val: continue
        if o == 'L':
            val[p] = ('W', d+1); queue.append(p)
        else:  # s is W for its mover -> bad for p
            remaining[p] -= 1
            maxchild[p] = max(maxchild[p], d)
            if remaining[p] == 0:
                val[p] = ('L', maxchild[p]+1); queue.append(p)

def name(s):
    return val.get(s, ('N', None))

print("initial", name(start))
for c, w, ns in succ[start]:
    o, d = name(ns)
    # value from X's perspective: successor is from O's perspective
    xo = {'W':'Loss','L':'Win','N':'NoForcedWin'}[o]
    print(" open", c, xo, (d+1) if d else None)
from collections import Counter
cnt = Counter(v[0] for v in val.values())
print("W", cnt['W'], "L", cnt['L'], "N", len(succ)-len(val))
dists = Counter(v[1] for v in val.values())
print("maxW", max(d for o,d in val.values() if o=='W'), "maxL", max(d for o,d in val.values() if o=='L'))
print("dist", sorted(dists.items()))
# O-starts symmetry
print("O starts:", name(((),(),1)) if ((),(),1) in succ else "not explored (expected symmetric)")

# Play out the forced line: winner picks shortest win, loser picks longest defense.
s = start; ply = 0; visited = []
line = []
while True:
    visited.append(s)
    ms = succ[s]
    o, d = name(s)
    wins = [c for c,w,ns in ms if w]
    if o == 'W' and wins:
        ply += 1; line.append(wins[0]); print("winning move at ply", ply, "cell", wins[0]); break
    if o == 'W':
        cands = [(name(ns)[1], c, ns) for c,w,ns in ms if not w and name(ns)[0]=='L']
        dd, c, ns = min(cands)
    else:
        cands = [(name(ns)[1], c, ns) for c,w,ns in ms if not w]
        dd, c, ns = max(cands)
    ply += 1; line.append(c); s = ns
print("line", line, "plies", ply, "repeated positions:", len(visited) != len(set(visited)))
