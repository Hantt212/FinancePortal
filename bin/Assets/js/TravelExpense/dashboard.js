
$.get('/TravelExpense/GetDashboardStats', function (res) {
    renderBudgetUsageChart(res.used, res.remaining);
    renderRequestStatusChartFromList(res.statuses);
});

let budgetChartInstance = null;
function renderBudgetUsageChart(used, remaining) {
    const ctx = document.getElementById('budgetUsageChart').getContext('2d');
    if (budgetChartInstance) {
        budgetChartInstance.destroy();
    }
    budgetChartInstance = new Chart(ctx, {
        type: 'pie',
        data: {
            labels: ['Used', 'Remaining'],
            datasets: [{
                data: [used, remaining],
                backgroundColor: ['#dc3545', '#28a745'],
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: {
                    position: 'bottom'
                }
            }
        }
    });
}


let statusChartInstance = null;
function renderRequestStatusChartFromList(statusList) {
    const ctx = document.getElementById('requestStatusChart').getContext('2d');
    if (statusChartInstance) {
        statusChartInstance.destroy();
    }

    const labels = statusList.map(s => s.label);
    const data = statusList.map(s => s.value);
    const colors = statusList.map(s => s.color);

    statusChartInstance = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Requests',
                data: data,
                backgroundColor: colors
            }]
        },
        options: {
            responsive: true,
            scales: {
                y: { beginAtZero: true }
            }
        }
    });
}
