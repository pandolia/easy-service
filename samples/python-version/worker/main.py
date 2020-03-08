import threading
import time
import datetime
import sys

def log(s):
    t = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    sys.stdout.write('[%s] %s\n' % (t, s))
    sys.stdout.flush()

def loop():
    log('Started SampleWorker(python version), press enter to exit')
    while running:
        log('Running')
        time.sleep(1)
    log('Stopped SampleWorker(python version)')

running = True
th = threading.Thread(target=loop)
th.start()

try:
    msg = raw_input()
except:
    msg = input()

log('Received message "%s" from the Monitor' % msg)

running = False
th.join()