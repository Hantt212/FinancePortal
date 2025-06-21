$(document).ready(function () {

});


function openPayment() {
    // Validate
    var exchangeRate = +$("#exchangeRate").val();
    if (exchangeRate == 0) {
        showToast("Exchange Rate cannot be empty.", "warning");
        return;
    }
    clearPayment();

    var ciaId = $("#ciaID").val();
    $.get(`/ExpenseClaim/GetInitPayment?ciaId=${ciaId}`, function (list) {
        // Payment Dropdown
        var paymentHtml = `<option value="">-- Select Value --</option>`;
        $('#vPaymentType').append(`<option value="">-- Select Value --</option>`);
        list.Payments.forEach(item => {
            paymentHtml += `<option value="${item.PaymentName}" id="pay${item.ID}" >${item.PaymentName}</option>`;
        });
        $('#vPaymentType').html(paymentHtml);


        const costGroup = new Map();
        // Group by CostName
        list.Costs.forEach(item => {
            const key = item.CostName;
            if (!costGroup.has(key)) {
                costGroup.set(key, []);
            }
            costGroup.get(key).push(item);
        });

        $('#vExpenseType').append(`<option value="">-- Select Value --</option>`);
        costGroup.forEach((costDetailList, key) => {
            $('#vExpenseType').append(`<option value="${key}" >${key}</option>`);
        });


        $('#vExpenseType').on('change', function () {
            const selectedKey = $(this).val();
            if (selectedKey == "") {
                $('#containerBudget').css('visibility', 'hidden');
            } else {
                $('#containerBudget').css('visibility', 'visible');
                $('#vBudgetLine').html('');

                const selectedItem = costGroup.get(selectedKey);
                if (selectedItem) {
                    $.each(selectedItem, function (index, budget) {
                        $('#vBudgetLine').append(`<option value="${budget.BudgetName}" id="budget${budget.BudgetID}">${budget.BudgetName}</option>`);
                    });
                }
            }
            
        });
        $("#addPaymentModal").modal("show");
    });
}

$('#vActualVND').on('input', function () {
    var exchangeRate = +$("#exchangeRate").val();
    var actualVND = +this.value; 
    var actualUSD = roundTo2Decimals(actualVND * exchangeRate);
    $("#vActualUSD").val(actualUSD);
});

function clearPayment() {
    $("#vPaymentType").val("");
    $("#vDescription").val("");
    $("#vExpenseType").val("");
    $("#vBudgetLine").val("");
    $("#vActualVND").val("");
    $("#vActualUSD").val("");

    $("#vPaymentType").html("");
    $("#vExpenseType").html("");
    $("#vBudgetLine").html("");
}

function addPaymentToTable(emp) {
    const rowHtml = `
        <tr>
            <td>${emp.Code}</td>
            <td>${emp.Name}</td>
            <td>${emp.Position}</td>
            <td>${emp.Department}</td>
            <td>${emp.Division}</td>
            <td class="text-center">
                <button class="btn btn-sm btn-danger w-auto remove-employee">
                    <i class="fa fa-trash"></i> <span class="d-none d-sm-inline">Remove</span>
                </button>
            </td>
        </tr>
    `;
    $('#paymentListTable tbody').append(rowHtml);
}

$('#addPaymentBtn').click(function () {
    var exchangeRate = +$("#exchangeRate").val();
    var paymentType = $("#vPaymentType").val();
    var description = $("#vDescription").val();
    var expenseType = $("#vExpenseType").val();
    var budgetLine = $("#vBudgetLine").val();
    var actualVND = $("#vActualVND").val();
    var actualUSD = $("#vActualUSD").val();
    // ✅ Validation
    if (!paymentType) {
        showToast("Payment Type cannot be empty.", "warning");
        return;
    }
    if (!description) {
        showToast("Description cannot be empty.", "warning");
        return;
    }
    if (!expenseType) {
        showToast("Expense Type cannot be empty.", "warning");
        return;
    }
    if (!budgetLine) {
        showToast("Budget Line cannot be empty.", "warning");
        return;
    }
    if (!actualVND) {
        showToast("Actual amm VND cannot be empty.", "warning");
        return;
    }
    
    var rowHtml = `
        <tr>
            <td>${paymentType}</td>
            <td>${description}</td>
            <td>${expenseType}</td>           
            <td>${budgetLine}</td>
            <td>${actualVND}</td>
            <td>${actualUSD}</td>
            <td class="text-center">
                <button class="btn btn-sm btn-danger w-auto remove-payment">
                    <i class="fa fa-trash"></i> <span class="d-none d-sm-inline">Remove</span>
                </button>
            </td>
        </tr>`;

    $('#paymentListTable tbody').append(rowHtml);
    
    $('#addPaymentModal').modal('hide');
    calcSumActualGroupPayment();
    
    // ✅ Show toast
    showToast("Payment added to the list successfully!", "success");

});


function calcSumActualGroupPayment() {
    const groupedSums = {};

    // 🔸 Remove old summary rows
    $('#paymentListTable tbody tr.summary-row, #paymentListTable tbody tr.blank-row').remove();

    // Loop through each actual data row
    $('#paymentListTable tbody tr').each(function () {
        const payment = $(this).find('td:eq(0)').text().trim();
        const actualVND = parseFloat($(this).find('td:eq(4)').text().replace(/,/g, '')) || 0;
        const actualUSD = parseFloat($(this).find('td:eq(5)').text().replace(/,/g, '')) || 0;

        // Skip total rows accidentally included
        if (isNaN(actualVND) || isNaN(actualUSD)) return;

        if (!groupedSums[payment]) {
            groupedSums[payment] = {
                actualVND: 0,
                actualUSD: 0
            };
        }

        groupedSums[payment].actualVND += actualVND;
        groupedSums[payment].actualUSD += actualUSD;
    });

    const $tbody = $('#paymentListTable tbody');

    // 🔸 Add two blank rows for spacing
    for (let i = 0; i < 2; i++) {
        $tbody.append(`
            <tr class="blank-row">
                <td>&nbsp;</td><td>&nbsp;</td><td>&nbsp;</td>
                <td>&nbsp;</td><td>&nbsp;</td><td>&nbsp;</td><td>&nbsp;</td>
            </tr>
        `);
    }

    // 🔸 Add totals grouped by payment type
    var totalActualUSD = 0;
    for (const [payment, totals] of Object.entries(groupedSums)) {
        const rowHtml = `
            <tr class="summary-row">
                <td>${payment}</td>
                <td class="font-weight-bold">Total ${payment}</td>
                <td></td>
                <td></td>
                <td class="font-weight-bold">${totals.actualVND}</td>
                <td class="font-weight-bold">${totals.actualUSD.toFixed(2)}</td>
                <td></td>
            </tr>
        `;
        $tbody.append(rowHtml);

        totalActualUSD += +totals.actualUSD.toFixed(2);
    }
    totalActualUSD = roundTo2Decimals(totalActualUSD);
    $("#totalExpense").html(totalActualUSD);

    calcSumByBudgetLine();

    // calc Balance company or Balance Employee
    var cashReceived = +$("#cashReceived").text().trim();
    const cashAdvanceUSD = +$('#paymentListTable tbody tr').filter(function () {
        return $(this).find('td:nth-child(2)').text().trim() === 'Total Cash Advance';
    }).find('td:nth-child(6)').text().trim();

    cashReceived = roundTo2Decimals(cashReceived);

    if (cashReceived > cashAdvanceUSD) {
        let balance = cashReceived - cashAdvanceUSD;
        $("#balanceCompany").text(balance);
        $("#balanceEmp").text("0");
    } else {
        let balance = cashAdvanceUSD - cashReceived;
        $("#balanceCompany").text("0");
        $("#balanceEmp").text(balance);
    }

    //
    let balanceCompany = roundTo2Decimals(+$("#balanceCompany").text())
    let totalCharges = (+totalActualUSD) - (+balanceCompany);
    $("#totalCharges").text(roundTo2Decimals(totalCharges));
}


function calcSumByBudgetLine() {
    const totalsByBudgetLine = {};

    $('#paymentListTable tbody tr').each(function () {
        const budgetLine = $(this).find('td:nth-child(4)').text().trim();
        const actualUSD = parseFloat($(this).find('td:nth-child(6)').text().trim()) || 0;

        if (!budgetLine) return; // Skip empty budget lines

        if (!totalsByBudgetLine[budgetLine]) {
            totalsByBudgetLine[budgetLine] = 0;
        }
        totalsByBudgetLine[budgetLine] += actualUSD;
    });

    // Clear existing rows before appending new ones (optional)
    $('#adjustBudgetTable tbody').empty();

    const budgetList = window.preloadedBudgetApproved;

    $.each(totalsByBudgetLine, function (budgetLine, totalUSD) {
        const matchedItem = budgetList.find(b => b.BudgetName === budgetLine);

        const approved = matchedItem ? matchedItem.BudgetAmount : 0;
        const actual = totalUSD.toFixed(2);
        const adjust = approved - actual

        const rowHtml = `
            <tr class="summary-row">
                <td>${budgetLine}</td>
                <td>${approved}</td>
                <td>${actual}</td>
                <td>${adjust}</td>
            </tr>
        `;
        $('#adjustBudgetTable tbody').append(rowHtml);
    });
}


$(document).on('click', '.remove-payment', function () {
    $(this).closest('tr').remove();
    calcSumActualGroupPayment();
});

function roundTo2Decimals(num) {
    return Math.round(num * 100) / 100;
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

