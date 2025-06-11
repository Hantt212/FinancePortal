$(document).ready(function () {
    loadBudgetTable();
    $('#saveBudgetBtn').click(function () {
        const id = $('#editBudgetId').val();
        const name = $('#newBudgetName').val().trim();
        const rawAmount = $('#newBudgetAmount').val().replace(/\./g, '').replace(/,/g, '');
        const amount = parseFloat(rawAmount) || 0;

        if (!name || amount <= 0) {          
            showToast("Please enter a valid budget name and amount.", "warning");
            return;
        }

        // Get Cost Checked List
        var costIDList = [];
        $('#costContainer input[type="checkbox"]:checked').each(function () {
            let costID = $(this).attr('id').replace('cost_', ''); // get ID without the "cost_" prefix
            costIDList.push(+costID);
        });

        const payload = {
            BudgetID: id ? parseInt(id) : 0,
            BudgetName: name,
            BudgetAmount: amount,
            CostIDList: costIDList
        };

        $.ajax({
            url: '/TravelExpense/AddBudget', // will reuse this, let backend handle insert/update
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            success: function (res) {
                if (res.success) {
                    $('#addBudgetModal').modal('hide');
                    loadBudgetTable();
                } else {
                    showToast(res.message || "Failed to save budget.", "danger");
                }
            }
        });
    });

    $('#addBudgetModal').on('hidden.bs.modal', function () {
        $('#newBudgetName').val('');
        $('#newBudgetAmount').val('');
    });

    $('#newBudgetAmount').on('input', function () {
        let raw = $(this).val().replace(/\./g, '').replace(/,/g, '');
        let num = parseFloat(raw) || 0;
        $(this).val(num.toLocaleString('de-DE'));
    });
});

//$('#addBudgetModal').on('hidden.bs.modal', function () {
//    $('#editBudgetId').val('');
//    $('#newBudgetName').val('');
//    $('#newBudgetAmount').val('');
//    $('#budgetModalTitle').text('Add New Budget');
    
//});

$(document).on('click', '.new-budget', function () {
    $('#editBudgetId').val('');
    $('#newBudgetName').val('');
    $('#newBudgetAmount').val('');
    $('#costContainer').html('');
    $('#budgetModalTitle').text('Add New Budget');
    loadCostInfo(0);
});

$(document).on('click', '.edit-budget', function () {
    const rowData = $('#budgetTable').DataTable().row($(this).closest('tr')).data();

    if (!rowData) {
        console.warn('Row data not found.');
        return;
    }

    $('#editBudgetId').val(rowData.ID);
    $('#newBudgetName').val(rowData.BudgetName);
    $('#newBudgetAmount').val(formatNumber(rowData.BudgetAmount));
    $('#budgetModalTitle').text('Edit Budget');
    loadCostInfo(rowData.ID)

    $('#addBudgetModal').modal('show');
});


function loadCostInfo(budgetID) {
    $.get(`/TravelExpense/GetAllCosts?budgetID=${budgetID}`, function (data) {
        if (data && data.length > 0) {
            var html = ``;
            for (var i = 0; i < data.length; i += 2) {
                html += `
            <div class="d-flex gap-4 mb-2 mt-3">
                <div class="form-check d-flex align-items-center" style="width: 50%">
                    <input type="checkbox" ${data[i].Checked ? 'checked' : ''} class="form-check-input custom-size me-2" id="cost_${data[i].CostID}">
                    <label class="form-check-label ml-4" for="cost_${data[i].CostID}">${data[i].CostName}</label>
                </div>`;

                if (i + 1 < data.length) {
                    html += `
                <div class="form-check d-flex align-items-center" style="width: 50%">
                    <input type="checkbox" ${data[i + 1].Checked ? 'checked' : ''} class="form-check-input custom-size me-2" id="cost_${data[i + 1].CostID}">
                    <label class="form-check-label ml-4" for="cost_${data[i + 1].CostID}">${data[i + 1].CostName}</label>
                </div>`;
                } else {
                    html += `<div style="width: 50%"></div>`; // filler if odd number
                }

                html += `</div>`;
            }
            $('#costContainer').html(html);
        }
    });

}
function loadBudgetTable() {
    $.get('/TravelExpense/GetAllBudgets', function (data) {
        // Destroy if exists
        if ($.fn.DataTable.isDataTable('#budgetTable')) {
            $('#budgetTable').DataTable().clear().destroy();
        }

        // Initialize fresh with new data
        $('#budgetTable').DataTable({
            data: data,
            bDestroy: true,
            order: [[0, "desc"]],
            dom: 'Bfrtip',
            buttons: ['copy', 'csv', 'excel', 'pdf', 'print'],
            columns: [
                { data: 'BudgetName' },
                {
                    data: 'BudgetAmount',
                    render: function (data) {
                        return formatNumber(data);
                    }
                },
                {
                    data: 'BudgetUsed',
                    render: function (data) {
                        return formatNumber(data);
                    }
                },
                {
                    data: 'BudgetRemaining',
                    render: function (data) {
                        return formatNumber(data);
                    }
                },
                {
                    data: 'CreatedDate',
                    render: function (data) {
                        return formatJSONDate(data);
                    }
                },
                {
                    data: 'IsShown',
                    render: function (data) {
                        return data ? 'Yes' : 'No';
                    }
                },
                {
                    data: null,
                    orderable: false,
                    render: function (data, type, row) {
                        return `
                <a class="btn btn-sm btn-outline-primary edit-budget" data-id="${row.ID}">
                    <i class="fa fa-edit"></i> Edit
                </a>

            `;
                    }
                }
            ]
        });
    });
}

function formatNumber(num) {
    return (parseFloat(num) || 0).toLocaleString('de-DE');
}

function formatJSONDate(jsonDate) {
    if (!jsonDate) return "N/A";

    let timestamp = parseInt(jsonDate.replace(/[^0-9]/g, ""), 10);
    let date = new Date(timestamp);

    let day = String(date.getDate()).padStart(2, "0");
    let month = String(date.getMonth() + 1).padStart(2, "0"); // Months are 0-based
    let year = date.getFullYear();
    let hours = String(date.getHours()).padStart(2, "0");
    let minutes = String(date.getMinutes()).padStart(2, "0");

    return `${year}-${month}-${day} ${hours}:${minutes}`;
}

function showToast(message, type = "success") {
    var toastEl = $("#toastMessage");

    let iconColor;
    let toastTitle;

    switch (type) {
        case "success":
            iconColor = "green";
            toastTitle = "Success";
            break;
        case "danger":
            iconColor = "red";
            toastTitle = "Error";
            break;
        case "info":
            iconColor = "blue";
            toastTitle = "Info";
            break;
        case "warning":
            iconColor = "orange";
            toastTitle = "Warning";
            break;
        default:
            iconColor = "gray";
            toastTitle = "Notification";
    }

    $("#toastIcon").css("background-color", iconColor);
    $("#toastTitle").text(toastTitle);
    $("#toastTime").text("Just now");
    $("#toastBody").text(message);

    toastEl.toast('show');
}