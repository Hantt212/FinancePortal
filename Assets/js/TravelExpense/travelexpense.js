
// 🔄 Init all fields for new/edit
$(document).ready(function () {
    // Generate TAR Number
    if ($('#tarNumber').text() == "" || $('#tarNumber').text() == null) {
        $.get('/TravelExpense/GenerateTARNo', function (res) {
            $('#tarNumber').text(res.tarNo);
        });
    }

    // 🖱️ Double click Exchange Rate to edit
    $('#ExchangeRate').on('dblclick', function () {
        $(this).prop('readonly', false);
    });

    // 🖱️ Blur Exchange Rate back to readonly + format
    $('#ExchangeRate').on('blur', function () {
        $(this).prop('readonly', true);
        formatExchangeRate();
        updateEstimatedCost();
    });

    // 🧮 Format Exchange Rate at first
    formatExchangeRate();

    // 🔄 Load Budgets
    loadCostBudget();

    // ⚡ Preload data for Edit mode
    if (window.isEdit) {
        // 🔹 Preload Employees
        if (window.preloadedEmployees?.length > 0) {
            window.preloadedEmployees.forEach(emp => {
                addEmployeeToTable(emp);
            });
        }

        if (window.preloadedExchangeRate) {
            $('#ExchangeRate').val(formatNumber(window.preloadedExchangeRate));
        }
        // 🔹 Preload Approver Info
        if (window.preloadedApprover) {
            $('#approverCode').val(window.preloadedApprover.Code || '');
            $('#approverName').val(window.preloadedApprover.Name || '');
            $('#approverEmail').val(window.preloadedApprover.Email || '');
            $('#approverPosition').val(window.preloadedApprover.Position || '');
            $('#approverSign').val(window.preloadedApprover.Name?.toLowerCase() || '');

            $('#approverInfoFields').slideDown();
        }
        
    }
    else {
        // 🆕 Only if it's a new TravelExpense, reset costs to 0
        $('.cost-input').val(0);
        updateEstimatedCost(); // because cost is 0, estimatedCost must be 0 too

        $.get('/TravelExpense/GetRequesterPreloadInfo', function (res) {
            if (res.success) {
                let filled = false;
                if (res.requesterSign) {
                    $('#requesterSign').val(res.requesterSign);
                    $('#requesterSignDate').val(res.requesterSignDate || '');
                }

                if (res.approver) {
                    $('#approverCode').val(res.approver.code || '');
                    $('#approverName').val(res.approver.name || '');
                    $('#approverEmail').val(res.approver.email || '');
                    $('#approverPosition').val(res.approver.position || '');
                    $('#approverSign').val(res.approver.signature || '');
                    $('#approverInfoFields').slideDown();

                    filled = true;
                }
                if (filled) {
                    showToast("Preloaded requester and/or approver info.", "info");
                } else {
                    showToast("No recent requester record found. Starting blank.", "warning");
                }
            } else {
                showToast("Unable to preload requester info.", "danger");
            }
        }).fail(function () {
            showToast("Server error while fetching preload info.", "danger");
        });
    }
    
});

// Change budget type
$(document).on('change', '.type-budget', function () {
    var selectId = $(this).attr('id');             
    var rowSelected = $(this).attr('positionrow');
    var costBudgetID = $(this).val();  
    
    var costBudgetIDList = [];
    costBudgetIDList.push(costBudgetID);
    // Optional: AJAX request
    $.ajax({
        url: '/TravelExpense/GetBudgetDetailByCostBudget',
        type: 'POST',
        contentType: 'application/json; charset=utf-8',
        data: JSON.stringify({ costBudgetIDList: costBudgetIDList }),
        success: function (res) {
            if (res.success) {
                $('#amount' + rowSelected).val(res.data[0].BudgetAmount || 0);
                $('#used' + rowSelected).val(res.data[0].BudgetUsed || 0);
                $('#remain' + rowSelected).val(res.data[0].BudgetRemaining || 0);
            } else {
                showToast(res.message || "Get budget information failed.", "danger");
            }
        },
        error: function () {
            showToast("Error retrieving budget info.", "danger");
        }
    });
});


$('#confirmAddEmployeeBtn').click(function () {
    if (!selectedEmployee) return;
    //Check code duplicate, don't insert
    var currEmpList = [];
    $('#employeeListTable tbody tr').each(function () {
        var firstTd = $(this).find('td:first');
        currEmpList.push(firstTd.text());
    });

    if (currEmpList.includes(selectedEmployee.Code) > 0) {
        showToast("Employee is duplicated in list !", "danger");
    } else {
        var rowHtml = `
        <tr>
            <td>${selectedEmployee.Code}</td>
            <td>${selectedEmployee.Name}</td>
            <td>${selectedEmployee.Position}</td>           
            <td>${selectedEmployee.Department}</td>
            <td>${selectedEmployee.Division}</td>
            <td class="text-center">
                <button class="btn btn-sm btn-danger w-auto remove-employee">
                    <i class="fa fa-trash"></i> <span class="d-none d-sm-inline">Remove</span>
                </button>
            </td>
        </tr>
    `;

        $('#employeeListTable tbody').append(rowHtml);
        $('#addEmployeeModal').modal('hide');
        $('#employeeCodeInput').val('');
        $('#employeeCardContainer').hide();
        selectedEmployee = null;

        // ✅ Show toast
        showToast("Employee added to the list successfully!", "success");
    }
    
});

$(document).on('click', '.remove-employee', function () {
    $(this).closest('tr').remove();
});

$('#approverCode').on('input', function () {
    const code = $(this).val().trim();
    if (!code || code.length < 5) {
        $('#approverInfoFields').hide();
        $('#approverName, #approverEmail, #approverPosition, #approverSign').val('');
        return;
    }

    $.ajax({
        url: '/TravelExpense/GetHodByCode',
        data: { code },
        success: function (res) {
            if (res.success) {
                $('#approverName').val(res.data.Name);
                $('#approverPosition').val(res.data.Position);
                $('#approverEmail').val(res.data.Email || '');
                $('#approverSign').val(res.data.Name?.toLowerCase() || '');
                $('#approverInfoFields').slideDown();
            } else {
                $('#approverInfoFields').hide();
                showToast(res.message || "Invalid approver code.", "danger");
            }
        },
        error: function () {
            $('#approverInfoFields').hide();
            showToast("Error retrieving approver info.", "danger");
        }
    });
});

var selectedEmployee = null;

$('#employeeCodeInput').on('input', function () {
    const code = $(this).val().trim();
    
    if (!code || code.length < 5) {
        $('#employeeCardContainer').hide();       
        selectedEmployee = null;       
        return;
    }

    $.ajax({
        url: '/TravelExpense/GetEmployeeByCode',
        data: { code },
        success: function (res) {
            if (res.success) {
                selectedEmployee = res.data;

                $('#empImage').attr('src', res.data.EmployeeImage);
                $('#empName').text(res.data.Name);
                $('#empCode').text(res.data.Code);
                $('#empPosition').text(res.data.Position);
                $('#empDivision').text(res.data.Division);
                $('#empDepartment').text(res.data.Department);
                $('#employeeCardContainer').show();
                showToast("Employee found: " + res.data.Name, "success");                
            } else {
                $('#employeeCardContainer').hide();
                showToast("Employee not found.", "danger");
                selectedEmployee = null;
            }
        },
        error: function () {
            $('#employeeCardContainer').hide();
            showToast("Server error while fetching employee info.", "danger");
            selectedEmployee = null;
        }
    });
});

document.getElementById('AttachmentFiles').addEventListener('change', function () {
    const fileList = document.getElementById('fileList');

    // Get existing file names from the list
    const existingFiles = Array.from(fileList.querySelectorAll('a'))
        .map(a => a.textContent.trim());

    Array.from(this.files).forEach((file) => {
        if (existingFiles.includes(file.name)) {
            console.warn(`File "${file.name}" is already added.`);
            return; // Skip duplicate
        }

        const li = document.createElement('li');
        li.className = 'list-group-item d-flex justify-content-between align-items-center border-bottom';

        // Create anchor with file name
        const hrefSpan = document.createElement('a');
        hrefSpan.textContent = file.name;
        //hrefSpan.href = '/Upload/' + encodeURIComponent(file.name);
        hrefSpan.style.color = '#007bff';
        hrefSpan.setAttribute('target', '_blank');

        // Remove button
        const removeBtn = document.createElement('button');
        removeBtn.className = 'btn btn-sm btn-danger ms-2';
        removeBtn.innerHTML = '<i class="fa fa-trash me-1"></i><span>Remove</span>';
        removeBtn.style.cursor = 'pointer';

        removeBtn.onclick = () => {
            li.remove();
        };

        // Append to list
        li.appendChild(hrefSpan);
        li.appendChild(removeBtn);
        fileList.appendChild(li);
    });
});

document.querySelectorAll('#fileList .btn-danger').forEach(btn => {
    btn.onclick = function () {
        this.closest('li')?.remove();
    };
});


$('#submitTravelBtn').click(function () {
    const fromDate = $('#BusinessDateFrom').val().trim();
    const toDate = $('#BusinessDateTo').val().trim();
    const tripDays = $('#TripDays').val().trim();
    const requestDate = $('#RequestDate').val().trim();
    const tripPurpose = $('#TripPurpose').val().trim();
    const tarNo = $('#tarNumber').text().trim();
    const requestID = parseInt($('#RequestID').val()) || 0;
    const statusID = parseInt($('#travelStatusID').val()) || 0;

    const estimatedCostRaw = $('#EstimatedCost').val().replace(/\./g, '').replace(/,/g, '');
    const exchangeRateRaw = $('#ExchangeRate').val().replace(/\./g, '').replace(/,/g, '');
    const estimatedCost = parseFloat(estimatedCostRaw) || 0;
    const exchangeRate = parseFloat(exchangeRateRaw) || 0;

    const budgetID = parseInt($('#BudgetName').val()) || 0;

    const approverCode = $('#approverCode').val().trim();
    const approverName = $('#approverName').val().trim();
    const approverEmail = $('#approverEmail').val().trim();
    const requesterSign = $('#requesterSign').val().trim();
    const employeeRows = $('#employeeListTable tbody tr');
    const attachmentFileRows = $("#fileList li");

    // ✅ Validation
    if (!tarNo) {
        showToast("TarNo cannot be empty.", "warning");
        return;
    }
    if (!fromDate) {
        showToast("Please enter Business From Date.", "warning");
        return;
    }
    if (!toDate) {
        showToast("Please enter Business To Date.", "warning");
        return;
    }
    if (fromDate > toDate) {
        showToast("From Date cannot be after To Date.", "warning");
        return;
    }
    if (!tripPurpose) {
        showToast("Please enter Trip Purpose.", "warning");
        return;
    }
    if (isNaN(estimatedCost) || estimatedCost <= 0) {
        showToast("Estimated Cost must be greater than 0.", "warning");
        return;
    }
    if (isNaN(exchangeRate) || exchangeRate <= 0) {
        showToast("Exchange Rate must be greater than 0.", "warning");
        return;
    }
    if (employeeRows.length === 0) {
        showToast("Please add at least one employee.", "warning");
        return;
    }
    if (!approverCode || !approverName || !approverEmail) {
        showToast("Please complete Approver Info before submitting.", "warning");
        return;
    }
    if (!requesterSign) {
        showToast("Requester Signature cannot be empty.", "warning");
        return;
    }

    // 👥 Collect employee list
    const employees = [];
    employeeRows.each(function () {
        const cols = $(this).find('td');
        employees.push({
            code: $(cols[0]).text().trim(),
            name: $(cols[1]).text().trim(),
            position: $(cols[2]).text().trim(),
            department: $(cols[3]).text().trim(),
            division: $(cols[4]).text().trim()
        });
    });

    //Attachment file
    const attachmentFileList = [];
    attachmentFileRows.each(function () {
        attachmentFileList.push($(this).find('a').text());
    })

    // Get CostDetail
    //var costDetailList = [];
    //document.querySelectorAll('#costCard .cost-input').forEach(item => {
    //    if (+item.value > 0) {
    //        var row = $(item).closest('.row');
    //        var selectElement = row.find('select');
    //        var costBudgetID = +selectElement.val();
    //        costDetailList.push({
    //            CostAmount: +item.value,
    //            CostBudgetID: costBudgetID
    //        })
    //    }
    //});
    //document.querySelectorAll('#costCard .cost-input').forEach(item => {
    //    if (+item.value > 0) {
    //        var row = $(item).closest('.row');
    //        var selectElement = row.find('select')[0]; // Get the DOM element from jQuery
    //        var selectedOption = selectElement.options[selectElement.selectedIndex];
    //        var costBudgetID = +selectedOption.value;
    //        var budgetID = +selectedOption.getAttribute('budgetid');
    //        var budgetRemain = +selectedOption.getAttribute('budgetremain');

   
    //        costDetailList.push({
    //            CostAmount: item.value,
    //            CostBudgetID: costBudgetID,
    //            BudgetID: budgetID // Add this if you want to include it
    //        });
    //    }
    //});

    const costDetailList = [];
    const usedBudgetIDs = new Set();

    let budgetError = false;
    try {

        document.querySelectorAll('#costCard .cost-input').forEach(item => {
            if (+item.value > 0) {
                const row = $(item).closest('.row');
                const selectElement = row.find('select')[0]; // Get the DOM element from jQuery
                const selectedOption = selectElement.options[selectElement.selectedIndex];
                const costBudgetID = +selectedOption.value;
                const budgetID = +selectedOption.getAttribute('budgetid');
                const budgetRemain = +selectedOption.getAttribute('budgetremain');
                const budgetName = selectedOption.getAttribute('budgetname');
                const currentAmount = +item.value;

                if (!usedBudgetIDs.has(budgetID)) {
                    usedBudgetIDs.add(budgetID);
                    if (currentAmount > budgetRemain) {
                        budgetError = true;
                        throw new Error(`Total cost for Budget ${budgetName} exceeds the remaining budget.`);
                    } else {
                        costDetailList.push({
                            CostAmount: currentAmount,
                            CostBudgetID: costBudgetID,
                            BudgetID: budgetID
                        });
                    }
                    
                } else {
                    // 1. Check sum CostAmount of budgetID
                    const existingTotal = costDetailList
                        .filter(detail => detail.BudgetID === budgetID)
                        .reduce((sum, detail) => sum + (+detail.CostAmount), 0);

                    const newTotal = existingTotal + currentAmount;

                    // 2. If greater than budgetRemain, show error; else add to costDetailList
                    if (newTotal > budgetRemain) {
                        budgetError = true;
                        throw new Error(`Total cost for Budget ${budgetName} exceeds the remaining budget.`);
                    } else {
                        costDetailList.push({
                            CostAmount: currentAmount,
                            CostBudgetID: costBudgetID,
                            BudgetID: budgetID
                        });
                    }
                }
            }
        });
    }catch(e) {
        showToast(e.message, "warning");
        return;
    }


    // 📦 Prepare final payload
    const payload = {
        id: parseInt(requestID),
        budgetID,
        tarNo,
        fromDate,
        toDate,
        statusID,
        tripDays,
        requestDate,
        tripPurpose,
        estimatedCost,
        exchangeRate,
        requesterSign,
        costDetails: costDetailList,
        approver: {
            code: approverCode,
            name: approverName,
            email: approverEmail,
            position: $('#approverPosition').val().trim()
        },
        employees: employees,
        attachmentFiles: attachmentFileList
    };



    // 🔄 Disable button while submitting
    const btn = $(this);
    btn.prop('disabled', true).html('<i class="fa fa-spinner fa-spin"></i> Submitting...');

    var formData = new FormData(document.getElementById('travelExpenseForm'));
    formData.append("Payload", JSON.stringify(payload));


    // 📤 Submit via AJAX
    $.ajax({
        url: '/TravelExpense/SubmitForm',
        type: 'POST',
        data: formData,
        processData: false, // important
        contentType: false, // important
        success: function (res) {
            if (res.success) {
                const msg = requestID > 0 ? "Travel Expense updated successfully!" : "Travel Expense created successfully!";
                showToast(msg, "success");

                setTimeout(() => {
                    window.location.href = '/TravelExpense/List';
                }, 100);
            } else {
                showToast(res.message || "Submission failed.", "danger");
                btn.prop('disabled', false).html('<i class="fa fa-save"></i> Submit');
            }

        },
        error: function () {
            showToast("Server error during submission.", "danger");
            btn.prop('disabled', false).html('<i class="fa fa-save"></i> Submit');
        }
    });
});

// 🧮 Calculate and update Estimated Cost
function updateEstimatedCost() {
    let totalUSD = 0;

    // Parse exchange rate with dot separator
    let exchangeRate = parseFloat($('#ExchangeRate').val().replace(/\./g, '').replace(',', '.')) || 0;

    $('.cost-input').each(function () {
        let val = parseFloat($(this).val()) || 0;
        val = Math.max(0, val);
        $(this).val(val);
        totalUSD += val;
    });

    const estimatedVND = totalUSD * exchangeRate;

    // Display with dot separator
    $('#EstimatedCost').val(formatNumber(estimatedVND));
}
function loadCostBudget() {
    $.get('/TravelExpense/GetCostBudgetList', function (list) {
        const costGroup = new Map();

        // Group by CostName
        list.forEach(item => {
            const key = item.CostName;
            if (!costGroup.has(key)) {
                costGroup.set(key, []);
            }
            costGroup.get(key).push(item);
        });

        const card = $('#costCard');
        card.empty();

        let index = 0;

        costGroup.forEach((costDetailList, key) => {
            let options = '';

            costDetailList.forEach(detail => {
                options += `<option value="${detail.ID}" budgetID="${detail.BudgetID}" budgetRemain="${detail.BudgetRemaining}" budgetName="${detail.BudgetName}">${detail.BudgetName}</option>`;
            });

            card.append(`
                <div class="form-group">
                    <div class="row">
                        <div class="col-md-2">
                            <div class="input-group">
                                <button class="input-group-prepend btn" style="width: 50%; text-align: left; background:#9ddef2" type="button">${key}</button>
                                <input type="number" class="form-control cost-input w-30" id="CostType_${index}" positionRow="${index}" placeholder="Amount ($)" value="0" />
                                <span class="input-group-text">($)</span>
                            </div>
                        </div>
                        <div class="col-md-4">
                            <select class="form-control type-budget" positionRow="${index}" id="BudgetType_${index}">
                                ${options}
                            </select>
                        </div>
                        <div class="col-md-6">
                            <div class="d-flex justify-content-between mb-3">
                                <div class="input-group" style="width: 32%;">
                                    <div class="input-group-prepend">
                                        <button class="btn btn-secondary" type="button">Amount</button>
                                    </div>
                                    <input type="text" class="form-control" value="0" id="amount${index}" readonly>
                                    <span class="input-group-text">($)</span>
                                </div>

                                <div class="input-group" style="width: 32%;">
                                    <div class="input-group-prepend">
                                        <button class="btn btn-warning" type="button">Used</button>
                                    </div>
                                    <input type="text" class="form-control" value="0" id="used${index}" readonly>
                                    <span class="input-group-text">($)</span>
                                </div>

                                <div class="input-group" style="width: 32%;">
                                    <div class="input-group-prepend">
                                        <button class="btn btn-success" type="button">Remain</button>
                                    </div>
                                    <input type="text" class="form-control" value="0" id="remain${index}" readonly>
                                    <span class="input-group-text">($)</span>
                                </div>
                            </div>

                        </div>
                    </div>
                </div>
            `);

            index++;
        });

        // Load preloaded values
        const costDetails = window.preloadedCostDetails || [];
        costDetails.forEach(detail => {
            $('.type-budget').each(function () {
                const $select = $(this);
                $select.find('option').each(function () {
                    if (this.value == detail.CostBudgetID) {
                        $(this).prop('selected', true);
                        const $input = $select.closest('.row').find('input[type="number"]');
                        $input.val(detail.CostAmount);
                    }
                });
            });
        });

        //row selected
        var costBudgetIDList = [];
        var rowList = [];

        $('select option:selected').each(function () {
            const selectElement = $(this).parent('select');
            const rowSelected = selectElement.attr('positionRow');
            const costBudgetID = $(this).val();

            costBudgetIDList.push(+costBudgetID);

            rowList.push({
                costBudgetID: +costBudgetID,
                rowSelected: rowSelected
            });
        });

        // Update corresponding amount, used, remain fields
        $.ajax({
            url: '/TravelExpense/GetBudgetDetailByCostBudget',
            type: 'POST',
            contentType: 'application/json; charset=utf-8',
            data: JSON.stringify({ costBudgetIDList: costBudgetIDList }),
            success: function (res) {
                if (res.success) {
                    var result = res.data;
                    result.forEach(item => {
                        // Find the matching row by costBudgetID
                        var row = rowList.find(r => r.costBudgetID === item.ID);

                        if (row) {
                            $('#amount' + row.rowSelected).val(item.BudgetAmount || 0);
                            $('#used' + row.rowSelected).val(item.BudgetUsed || 0);
                            $('#remain' + row.rowSelected).val(item.BudgetRemaining || 0);
                        }
                    });
                } else {
                    showToast(res.message || "Get budget information failed.", "danger");
                }
            },
            error: function () {
                showToast("Error retrieving budget info.", "danger");
            }
        });

        
    });
}


// 🎯 Bind input event to recalculate on change
$(document).on('input', '.cost-input', function () {
    updateEstimatedCost();
});

function formatNumber(num) {
    return num.toLocaleString('de-DE'); // e.g. 1.234.567
}

function formatExchangeRate() {
    let raw = $('#ExchangeRate').val().replace(/\./g, '').replace(',', '.');
    let num = parseFloat(raw) || 0;
    $('#ExchangeRate').val(formatNumber(num)); // dot separator
}

$('#ExchangeRate').on('input', function () {
    let raw = $(this).val().replace(/\./g, '').replace(',', '.');
    let num = parseFloat(raw) || 0;

    updateEstimatedCost();
    $(this).val(formatNumber(num)); // formatted with dot
});

function updateTripDays() {
    const fromStr = $('#BusinessDateFrom').val().trim();
    const toStr = $('#BusinessDateTo').val().trim();

    if (!fromStr || !toStr) {
        $('#TripDays').val('');
        return;
    }

    const fromDate = new Date(fromStr);
    const toDate = new Date(toStr);

    if (fromDate > toDate) {
        $('#TripDays').val('');
        showToast("From Date cannot be after To Date.", "warning");
        $('#BusinessDateFrom').focus();
        return;
    }

    const days = Math.ceil((toDate - fromDate) / (1000 * 60 * 60 * 24)) + 1;
    $('#TripDays').val(days);
}

function addEmployeeToTable(emp) {
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

    $('#employeeListTable tbody').append(rowHtml);
}

$('#BusinessDateFrom, #BusinessDateTo').on('change', updateTripDays);

//$('#saveBudgetBtn').click(function () {
//    const name = $('#newBudgetName').val().trim();
//    const amountRaw = $('#newBudgetAmount').val().replace(/\./g, '').replace(/,/g, '');
//    const amount = parseFloat(amountRaw) || 0;

//    if (!name || amount <= 0) {
//        showToast("Please enter valid budget name and amount.", "warning");
//        return;
//    }

//    $.ajax({
//        url: '/TravelExpense/AddBudget',
//        type: 'POST',
//        contentType: 'application/json',
//        data: JSON.stringify({ BudgetName: name, BudgetAmount: amount }),
//        success: function (res) {
//            if (res.success) {
//                $('#addBudgetModal').modal('hide');
//                showToast("Budget added successfully", "success");
//               // loadBudgets();
//            } else {
//                showToast(res.message || "Add budget failed", "danger");
//            }
//        }
//    });
//});

$('#newBudgetAmount').on('input', function () {
    let raw = $(this).val().replace(/\./g, '').replace(/,/g, '');
    let num = parseFloat(raw) || 0;
    $(this).val(num.toLocaleString('de-DE')); // Use dot for thousands
});

//$('#addBudgetModal').on('hidden.bs.modal', function () {
//    $('#newBudgetName').val('');
//    $('#newBudgetAmount').val('');
//});

$('#BudgetName').on('change', function () {
    const selected = $(this).find('option:selected');

    $('#BudgetAmount').val(formatNumber(selected.data('amount') || 0));
    $('#BudgetUsed').val(formatNumber(selected.data('used') || 0));
    $('#BudgetRemaining').val(formatNumber(selected.data('remaining') || 0));
});

//function resetTravelExpenseForm() {
//    $('#BusinessDateFrom, #BusinessDateTo, #RequestDate').val('');
//    $('#TripPurpose').val('');
//    $('#TripDays').val('');
//    $('#EstimatedCost').val('');
//    $('#ExchangeRate').val('25000');
//    $('#BudgetName').val('');
//    $('#BudgetAmount, #BudgetUsed, #BudgetRemaining').val('');
//    $('.cost-input').val(0);

//    // Clear employee table
//    $('#employeeListTable tbody').empty();

//    // Clear approver
//    $('#approverCode, #approverName, #approverEmail, #approverPosition').val('');
//    $('#approverSign').val('');
//    $('#approverInfoFields').hide();
//}

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
