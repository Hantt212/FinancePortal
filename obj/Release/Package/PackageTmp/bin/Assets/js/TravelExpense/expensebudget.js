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

        const payload = {
            ID: id ? parseInt(id) : 0,
            BudgetName: name,
            BudgetAmount: amount
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

$('#addBudgetModal').on('hidden.bs.modal', function () {
    $('#editBudgetId').val('');
    $('#newBudgetName').val('');
    $('#newBudgetAmount').val('');
    $('#budgetModalTitle').text('Add New Budget');
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

    $('#addBudgetModal').modal('show');
});

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
                <button class="btn btn-sm btn-outline-primary edit-budget" data-id="${row.ID}">
                    <i class="fa fa-edit"></i> Edit
                </button>

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