###
### Wacky code ahead! Written in like 10s so excuse inefficient programming please.
### Feel free to modify
### Database source: http://addresses.loyce.club/
###

import csv

targets = ""
outputfile = "wallets.ind"

with open(outputfile, "w") as f:
    f.write("")

def appendDB():
    global targets
    with open(outputfile, "a") as f:
        f.write(targets)
    targets = ""

path = input("CSV input path: ")
count = len(open(path).readlines())
done = 0
found = 0
donesincelastflush = 0

maxrows = int(input("Rows wanted: "))
enableMaxRows = True

with open(path, "r") as fd:
    rd = csv.reader(fd, delimiter="\t", quotechar='"')
    rows = 0
    if (not enableMaxRows):
        maxrows = count
    for row in rd:
        rows += 1
        if (rows != 1):
            addr = row[0]
            bal = row[1]
            if (not bal.isnumeric()):
                print("String bal: " + bal)
                continue

            bal = int(bal)

            done += 1
            
            if (bal > 250000 and found <= maxrows):
                targets += addr + "\n"
                found += 1
                donesincelastflush += 1
            else:
                appendDB()
                print("Done; balances get too low")
                break

            if(done % 10000 == 0):
                print("Done: " + str(round(done / maxrows * 100)) + "% | count: " + str(found) + " | est total: " + str(round( (found / done) * count )))
                appendDB()
                donesincelastflush = 0
        else:
            if (rows != 1):
                break

print("Done. Output addresses: " + str(found - 1))