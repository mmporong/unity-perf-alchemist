const express = require('express');
const app = express();
const path = require('path');
const PORT = 3848;

app.use(express.json());
app.use(express.static('public'));

let currentData = {
    initialFPS: 0,
    bestFPS: 0,
    improvement: 0,
    history: [],
    status: "Idle",
    lastUpdate: null
};

// 유니티로부터 데이터를 받는 엔드포인트
app.post('/api/update', (req, res) => {
    currentData = req.body;
    currentData.lastUpdate = new Date().toLocaleTimeString();
    console.log(`[Sync] Received Update from Unity: Gen ${currentData.history.length - 1}`);
    res.json({ success: true });
});

// 웹 프론트엔드용 데이터 조회 엔드포인트
app.get('/api/data', (req, res) => {
    res.json(currentData);
});

// 기본 페이지
app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

app.listen(PORT, () => {
    console.log(`🚀 Alchemist Web Dashboard running at http://localhost:${PORT}`);
});
