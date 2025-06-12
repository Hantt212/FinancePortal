$(document).ready(function () {
    loadUserRequests();
});

function loadUserRequests() {

    $.get('/TravelExpense/GetAllStatus', function (data) {
        if (data) {
            var statusItem = $("#statusFilter");
            statusItem.html('');
            statusItem.append(`<option value="">All</option>`);
            data.forEach(status => {
                statusItem.append(`<option value ="${status}">${status}</option>`)
            })
        }
    });

    $.get('/TravelExpense/GetAllList', function (data) {
        const table = $('#allListTbl').DataTable({
            data: data,
            bDestroy: true,
            order: [[2, "desc"]],
            dom: 'Bfrtip',
            buttons: ['copy', 'csv', 'excel', 'pdf', 'print'],
            columns: [
                {
                    className: 'child-toggle text-center',
                    orderable: false,
                    data: null,
                    defaultContent: '➕',
                    width: '30px',
                    createdCell: function (td, cellData, rowData, rowIndex, colIndex) {
                        // For example, use the row ID or TarNo to make a unique ID
                        $(td).attr('id', `${rowData.ID}`);
                    }
                },
                { data: 'Department' },
                { data: 'TarNo' },
                {
                    data: 'DisplayName',
                    render: function (data, type, row) {
                        if (type === 'display') {
                            const bgColor = row.ColorCode || '#6c757d';
                            return `<span class="badge" style="background-color: ${bgColor}; color: #fff; font-weight: 500;">${data}</span>`;
                        }
                        return data;
                    }
                }
            ]
        });

        // Toggle child row
        // Click handler to toggle child row
        $('#allListTbl tbody').on('click', 'td.child-toggle', function () {
            const tr = $(this).closest('tr');
            const row = table.row(tr);

            if (row.child.isShown()) {
                row.child.hide();
                $(this).text('➕');
            } else {
                const travelID = +row.data().ID;
                
                $(this).text('⏳');

                $.ajax({
                    url: '/TravelExpense/GetCurrentList',
                    data: { travelID },
                    success: function (res) {
                        if (res.success) {
                            let childHtml = `
                        <table class="table table-bordered">
                          <thead class="thead-dark">
                            <tr>
                              <th scope="col">Name</th>
                              <th scope="col">Request Date</th>
                              <th scope="col">Actions</th>
                            </tr>
                          </thead>
                          <tbody>`;

                            res.data.forEach(item => {
                                childHtml += `
                            <tr>
                              <th scope="row">${item.FormName}</th>
                              <td>${item.CreatedDate}</td>
                              <td>
                                <a class="btn btn-sm btn-outline-info btn-view-request" data-id="${item.ID}">
                                    <i class="fa fa-eye"></i> View
                                </a>`;

                                // Conditionally include CIA button
                                if (item.EditMode == 1) {
                                    if (item.FormName == "Cash In Advance") {
                                        childHtml += `
                                                        <a href="/CashInAdvance/Index?t=${encodeURIComponent(item.TokenID)}"
                                                           class="btn btn-sm btn-outline-success ml-1">
                                                            <i class="fa fa-edit"></i> Edit
                                                        </a>`;
                                    } else {
                                        childHtml += `
                                                        <a href="/TravelExpense/Index/${item.ID}" class="btn btn-sm btn-outline-primary ml-1">
                                                            <i class="fa fa-edit"></i> Edit
                                                        </a>`;
                                    }
                                    
                                }

                                childHtml += `</td></tr>`;
                            });

                            childHtml += `</tbody></table>`;

                            row.child(childHtml).show();
                            tr.find('td.child-toggle').text('➖');
                        } else {
                            showToast(res.message || "Invalid approver code.", "danger");
                            tr.find('td.child-toggle').text('➕');
                        }
                    },
                    error: function () {
                        showToast("Error retrieving approver info.", "danger");
                        tr.find('td.child-toggle').text('➕');
                    }
                });
            }
        });
    });

}

$(document).on('click', '.btn-view-request', function () {
    const requestId = $(this).data('id');
    if (!requestId) return;

    // Load request data and fill the modal
    loadRequestDetails(requestId);
});

function loadRequestDetails(requestId) {
    $.get(`/TravelExpense/GetRequestViewDetails?id=${requestId}`, function (data) {
        if (!data) {
            showToast("Failed to load request details", "danger");
            return;
        }

        // Fill modal with all info (including budget)
        fillViewModal(data);

        // ✅ Show the modal
        $('#viewRequestModal').modal('show');
    }).fail(function () {
        showToast("Server error while loading request details", "danger");
    });
}


function fillViewModal(data) {
    const role = sessionStorage.getItem("currentUserRole") || "";
    $('#viewRequestID').val(data.ID);
    // 🔹 Travel Info
    $('#viewTarNo').text(data.TarNo || "");
    $('#viewFromDate').text(formatJSONDate(data.FromDate));
    $('#viewToDate').text(formatJSONDate(data.ToDate));
    $('#viewTripDays').text(data.TripDays ?? "");
    $('#viewRequestDate').text(formatJSONDate(data.RequestDate));
    $('#viewTripPurpose').text(data.TripPurpose || "");


    // 🔹 Cost Details
    var costDetails = data.CostDetails;
    var costViewHtml = '';
    var itemsPerRow = 2;
    var index = 0;
    costDetails.forEach(function (cost, i) {
        // Start a new row every 4 items
        costViewHtml += `<div class="row mt-4">
                            <div class="col-md-3"><strong>Cost:</strong> <span>${cost.CostName}</span></div>
                            <div class="col-md-2"><span>${cost.CostAmount}$</span></div>
                            <div class="col-md-7">
                                <strong>Budget:</strong> <span>${cost.BudgetName}</span>
                                <div class="row mt-1">
                                    <div class="col-md-4"><strong class="font-italic text-secondary">Amount:</strong><span class="font-italic text-secondary">${cost.BudgetAmountAtSubmit}$</span></div>
                                    <div class="col-md-4"><strong class="font-italic text-secondary">Used:</strong><span class="font-italic text-secondary">${cost.BudgetUsedAtSubmit}$</span></div>
                                    <div class="col-md-4"><strong class="font-italic text-secondary">Remain:</strong> <span class="font-italic text-secondary">${cost.BudgetRemainAtSubmit}$</span></div>
                                </div>
                            </div>
                            
                        </div>`
    });

    costViewHtml += `<div class="row mt-3">
                        <div class="col-md-5 mt-3"><strong class="mr-2">Exchange Rate:</strong>${data.ExchangeRate ?? 0}</div>
                        <div class="col-md-7 mt-3"><strong class="mr-2">Estimated VND:</strong>${data.EstimatedCost || 0}</div>
                    </div>`;
    $('#costViewContainer').html(costViewHtml);


    // 🔹 Employees
    $('#viewEmployeeList').empty();
    (data.Employees || []).forEach(emp => {
        const item = `<li class="list-group-item">${emp.Name} - ${emp.Position}</li>`;
        $('#viewEmployeeList').append(item);
    });

    // Attachment File
    $('#ddAttachment').empty();
    (data.AttachmentFiles || []).forEach(name => {
        const file = `<li><a  style="color: #007bff" class="dropdown-item" href="/Upload/${name}" target="_blank">${name}</a></li>`
        $('#ddAttachment').append(file);
    });

    // 🔹 Requester Signature
    $('#viewRequesterSign').text(data.RequesterSign || "");
    $('#viewRequesterDate').text(formatJSONDate(data.CreatedDate));

    // 🔹 Approval Sections
    resetApprovalSections();

    const approvals = data.Approvals || [];
    showApprovalSections(role, approvals, data.StatusID);
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