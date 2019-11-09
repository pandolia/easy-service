function log(s) {
    var t = new Date();
    console.log(`[${t.toLocaleString()}] ${s}`);
}

log('Started SampleWorker(node.js version)')

var intv = setInterval(function () { log('Running'); }, 1000);

process.stdin.on('data', function (data) {
    log(`Received message "${data.toString().trim()}" from the Monitor`);
    clearInterval(intv);
    log('Stopped SampleWorker(node.js version)');
    process.exit();
});