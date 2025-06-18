
const StatusEnum = {
    Cancelled: 1,
    WaitingHOD: 2,
    RejectedHOD: 3,
    WaitingGL: 4,
    WaitingFC: 5,
    RejectedFC: 6,
    TARApproved: 7
};

const LabelStatusEnum = {
    Waiting: 'Waiting',
    Approved: 'Approved',
    Rejected: 'Rejected',
    NotAssigned: 'Not Assigned',
    NotReviewed: 'Not Reviewed'
}

const RoleEnum = {
    Requester: 'Requester',
    HOD: 'HOD',
    GL: 'GL',
    FC: 'FC',
}

const StepEnum = {
    HOD: 1,
    GL: 2,
    FC: 3
}

$(document).ready(function () {
    loadUserRequests();
});

// 🔍 View Button travel Expense
$(document).on('click', '.btn-view-request', function () {
    const requestId = $(this).data('id');
    if (!requestId) return;

    // Load request data and fill the modal
    loadRequestDetails(requestId);
});


$(document).on('change', '#statusFilter', function () {
    var table = $('#requestListTbl').DataTable();

    var val = $.fn.dataTable.util.escapeRegex($(this).val());
    table.column(5).search(val ? '^' + val + '$' : '', true, false).draw();
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

    $.get('/TravelExpense/GetUserRequests', function (data) {

        const table = $('#requestListTbl').DataTable({
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
                    width: '30px',
                    render: function (data, type, row) {
                        return row.EditCash ? '<i class="fa fa-caret-down"></i>' : '';
                    },
                    createdCell: function (td, cellData, rowData) {
                        $(td).attr('id', `${rowData.ID}`);
                        $(td).attr('data-expanded', 'false');
                    }
                },
                { data: 'Department' },
                {
                    data: 'TarNo',
                    render: function (data, type, row) {
                        return row.EditCash ? `${data}<i class="fa fa-money-bill-wave ml-3 text-success" data-bs-toggle="tooltip" data-bs-placement="top" title="${row.CashStatusName}"></i>` : `${data}`;
                    }, 
                },
                {
                    data: 'RequestDate',
                    render: function (data) {
                        //const match = /\/Date\((\d+)\)\//.exec(data);
                        return convertDate(data)
                        
                    }
                },
                {
                    data: 'EstimatedCost',
                    render: function (d) {
                        return parseFloat(d).toLocaleString('vi-VN');
                    }
                },
                {
                    data: 'DisplayName',
                    render: function (data, type, row) {
                        if (type === 'display') {
                            const bgColor = row.ColorCode || '#6c757d';
                            return `<span class="badge" style="background-color: ${bgColor}; color: #fff; font-weight: 500;">${data}</span>`;
                        }
                        return data;
                    }
                },
                {
                    data: null,
                    title: "Actions",
                    orderable: false,
                    render: function (data) {
                        const viewBtn = `<a class="btn btn-sm btn-outline-info btn-view-request" data-id="${data.ID}">
                                             <i class="fa fa-eye"></i>
                                         </a>`;
                        const editBtn = data.EditMode
                            ? `<a href="/TravelExpense/Index/${data.ID}" class="btn btn-sm btn-outline-primary ml-1">
                            <i class="fa fa-edit"></i>
                           </a>` : "";
                        const cashBtn = data.NewCash
                            ? `<a href="/CashInAdvance/Index?t=${encodeURIComponent(data.Token)}" class="btn btn-sm btn-outline-success ml-1">
                            ➕
                           </a>` : "";
                        return `${viewBtn} ${editBtn} ${cashBtn}`;
                    }
                }
            ]
        });

        // ✅ Re-initialize Bootstrap 5 tooltips after DataTable content is rendered
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.forEach(function (tooltipTriggerEl) {
            new bootstrap.Tooltip(tooltipTriggerEl);
        });

        // Action open drop down
        $('#requestListTbl tbody').off('click', 'td.child-toggle').on('click', 'td.child-toggle', function () {
            const tr = $(this).closest('tr');
            const row = table.row(tr);
            const $cell = $(this);

            const travelID = +$cell.attr('id');

            // Check if already expanded
            if ($cell.attr('data-expanded') === 'true') {
                // Remove custom row
                tr.next('tr.child-row').remove();
                $cell.html('<i class="fa fa-caret-down"></i>').attr('data-expanded', 'false');
            } else {
                $.ajax({
                    url: '/TravelExpense/GetCurrentList',
                    data: { travelID },
                    success: function (res) {
                        if (res.success && res.data.length > 0) {
                            let childHtml = `<table class="table table-sm table-bordered m-0">
                        <thead class="thead-light">
                            <tr>
                                <th>Form</th>
                                <th>Request Date</th>
                                <th>Status</th>
                                <th>Actions</th>
                            </tr>
                        </thead><tbody>`;

                            res.data.forEach(item => {
                                childHtml += `
                            <tr>
                                <td>${item.FormName}</td>
                                <td>${item.CreatedDate}</td>
                                <td>
                                    <span class="badge" style = "background-color: ${item.StatusColor}; color: #fff; font-weight: 500;" > ${item.StatusName }</span>
                                </td>
                                <td>`;

                                if (item.EditMode == 1) {
                                    if (item.FormName === "Cash In Advance") {
                                        childHtml += `
                                    <a class="btn btn-sm btn-outline-info btn-view-cia" data-id="${item.ID}"><i class="fa fa-eye"></i></a>
                                    <a href="/CashInAdvance/Index?t=${encodeURIComponent(item.TokenID)}" class="btn btn-sm btn-outline-primary ml-1">
                                        <i class="fa fa-edit"></i>
                                    </a>`;
                                    } else {
                                        childHtml += `<a href="/ClaimForm/Index/${item.ID}" class="btn btn-sm btn-outline-primary ml-1">
                                                        <i class="fa fa-edit"></i>
                                                    </a>`;
                                    }
                                }

                                childHtml += `</td></tr>`;
                            });

                            childHtml += `</tbody></table>`;

                            // Insert a full new row with second td spanning full table width minus 1
                            const colCount = tr.find('td').length;
                            const newRow = `<tr class="child-row">
                        <td></td>
                        <td colspan="${colCount - 1}">${childHtml}</td>
                    </tr>`;

                            $(newRow).insertAfter(tr);
                            $cell.html('<i class="fa fa-caret-up"></i>').attr('data-expanded', 'true');
                        } else {
                            showToast(res.message || "No data available.", "warning");
                            $cell.html('<i class="fa fa-caret-down"></i>').attr('data-expanded', 'false');
                        }
                    },
                    error: function () {
                        showToast("Failed to load child rows.", "danger");
                        $cell.html('<i class="fa fa-caret-down"></i>').attr('data-expanded', 'false');
                    }
                });
            }
        });

    });

 

}

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

    // SHow/Hide Container
    $("#containerTripInfo").show();
    $("#containerCostInfo").show();
    $("#containerEmpInfo").show();
    $("#containerRequireInfo").hide();
    $("#containerBankInfo").hide();
}

function resetApprovalSections() {
    $('#approvalActions').addClass('d-none');
    $('#hodSection').addClass('d-none');
    $('#fcSection').addClass('d-none');
    $('#cancelActions').addClass('d-none');
}

function showApprovalSections(role, approvals, statusID) {
    const hod = approvals.find(a => a.ApprovalStep === StepEnum.HOD);
    const gl = approvals.find(a => a.ApprovalStep === StepEnum.GL);
    const fc = approvals.find(a => a.ApprovalStep === StepEnum.FC);

    resetApprovalSections();

    // Show HOD Section
    if (hod) {
        showHODSection(hod, statusID);
    } else {
        setFallbackHODSection();
    }

    // Show FC Section (always shown for consistency)
    showFCSection(fc, statusID);

    // Approve/Reject buttons (based on role + section still pending)
    if (role === RoleEnum.HOD && statusID === StatusEnum.WaitingHOD) {
        $('#approvalActions').removeClass('d-none');
        $('#rejectBtn').show();
    } else if (role === RoleEnum.GL && statusID === StatusEnum.WaitingGL) {
        $('#approvalActions').removeClass('d-none');
        $('#rejectBtn').hide();
    } else if (role === RoleEnum.FC && statusID === StatusEnum.WaitingFC) {
        $('#approvalActions').removeClass('d-none');
        $('#rejectBtn').show();
    }

    //Cancel buttons
    if (role === RoleEnum.Requester && statusID < StatusEnum.RejectedHOD) {
        $('#cancelActions').removeClass('d-none');
    } else {
        $('#cancelActions').addClass('d-none');
    }
}

function showHODSection(hod, statusID) {
    $('#viewHODName').text(hod.ApproverName || "N/A");
    $('#viewHODEmail').text(hod.ApproverEmail || "N/A");
    $('#viewHODPosition').text(hod.ApproverPosition || "N/A");
    $('#viewHODSignature').text(hod.ApproverSignature || "N/A");
    $('#viewHODSignDate').text(formatJSONDate(hod.ApproverSignDate) || "N/A");

    let statusLabel = LabelStatusEnum.NotReviewed;

    if (statusID === StatusEnum.WaitingHOD) {
        statusLabel = LabelStatusEnum.Waiting;
    } else if (statusID === StatusEnum.RejectedHOD) {
        statusLabel = LabelStatusEnum.Rejected;
    } else if (statusID > StatusEnum.RejectedHOD) {
        statusLabel = LabelStatusEnum.Approved;
    }

    setApprovalStatusBadge("#hodApprovalStatus", statusLabel);
    $('#hodSection').removeClass('d-none');
}

function setFallbackHODSection() {
    $('#hodSection').removeClass('d-none');
    $('#viewHODName').text("N/A");
    $('#viewHODEmail').text("N/A");
    $('#viewHODPosition').text("N/A");
    $('#viewHODSignature').text("N/A");
    $('#viewHODSignDate').text("N/A");
    setApprovalStatusBadge('#hodApprovalStatus', LabelStatusEnum.NotAssigned);
}

function showFCSection(fc, statusID) {
    $('#viewFCName').text(fc?.ApproverName || "N/A");
    $('#viewFCEmail').text(fc?.ApproverEmail || "N/A");
    $('#viewFCPosition').text(fc?.ApproverPosition || "N/A");
    $('#viewFCSignature').text(fc?.ApproverSignature || "N/A");
    $('#viewFCSignDate').text(formatJSONDate(fc?.ApproverSignDate) || "N/A");

    let statusLabel = LabelStatusEnum.NotAssigned;

    if (statusID === StatusEnum.WaitingFC) {
        statusLabel = LabelStatusEnum.Waiting;
    } else if (statusID === StatusEnum.RejectedFC) {
        statusLabel = LabelStatusEnum.Rejected;
    } else if (statusID === StatusEnum.TARApproved) {
        statusLabel = LabelStatusEnum.Approved;
    }

    setApprovalStatusBadge("#fcApprovalStatus", statusLabel);
    $('#fcSection').removeClass('d-none');
}

function setApprovalStatusBadge(selector, status) {
    let badgeClass = 'badge-secondary';
    let label = LabelStatusEnum.NotAssigned;
    let backgroundColor = '#6c757d'; // Default: Not Assigned (secondary)

    switch (status) {
        case 'Approved':
            badgeClass = 'badge-success';
            label = 'Approved';
            backgroundColor = '#28a745';
            break;
        case 'Waiting':
            badgeClass = 'badge-warning';
            label = 'Waiting';
            backgroundColor = '#ffc107';
            break;
        case 'Rejected':
            badgeClass = 'badge-danger';
            label = 'Rejected';
            backgroundColor = '#dc3545';
            break;
        case LabelStatusEnum.NotReviewed:
            badgeClass = 'badge-info';
            label = LabelStatusEnum.NotReviewed;
            backgroundColor = '#17a2b8';
            break;
        case LabelStatusEnum.NotAssigned:
        default:
            // Keep default styles
            label = LabelStatusEnum.NotAssigned;
            break;
    }

    $(selector)
        .removeClass()
        .addClass(`badge ${badgeClass}`)
        .css({
            backgroundColor: backgroundColor,
            color: 'white'
        })
        .text(label);
}

let userAction = null;

$('#approveBtn').click(() => {
    userAction = 1;
    $('#confirmApprovalMessage').text("Are you sure you want to APPROVE this request?");
    $('#confirmApprovalModal').modal('show');
});

$('#rejectBtn').click(() => {
    userAction = 0;
    $('#confirmApprovalMessage').text("Are you sure you want to REJECT this request?");
    $('#confirmApprovalModal').modal('show');
});

$('#cancelBtn').click(() => {
    userAction = -1;
    $('#confirmApprovalMessage').text("Are you sure you want to CANCEL this request?");
    $('#confirmApprovalModal').modal('show');
});

$('#confirmApprovalBtn').click(() => {
    $('#confirmApprovalModal').modal('hide');
    submitApproval(userAction);
});


function submitApproval(userAction) {

    const role = sessionStorage.getItem("currentUserRole") || "";
    const requestId = $('#viewRequestID').val();

    if (!requestId) {
        showToast("Missing Request ID", "danger");
        return;
    }

    //Cancel TravelExpense Request
    if (userAction < 0) {
        $.ajax({
            url: '/TravelExpense/CancelByRequester',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                requestID: +requestId
            }),
            success: function (res) {
                if (res.success) {
                    showToast("Request cancelled successfully!", "success");

                    // Refresh modal contents in real-time
                    loadRequestDetails(requestId);
                } else {
                    showToast(res.message || "Failed to cancel request", "danger");
                }
            },
            error: function () {
                showToast("Server error while approving request", "danger");
            }
        });

    } else { //Approve or Reject TravelExpense Request


        const payload = {
            requestId: parseInt(requestId),
            isApprove: userAction == 0 ? false : true
        };


        let url;
        switch (role) {
            case RoleEnum.HOD:
                url = '/TravelExpense/ApproveByHOD';
                break;
            case RoleEnum.GL:
                url = '/TravelExpense/ApproveByGL';
                break;
            case RoleEnum.FC:
                url = '/TravelExpense/ApproveByFC';
                break;
            default:
                url = null;
        }

        if (!url) {
            showToast("Unauthorized action for current role", "warning");
            return;
        }

        $.ajax({
            url: url,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            success: function (res) {
                if (res.success) {
                    showToast("Approval status updated successfully!", "success");

                    // Refresh modal contents in real-time
                    loadRequestDetails(payload.requestId);
                } else {
                    showToast(res.message || "Failed to update approval", "danger");
                }
            },
            error: function () {
                showToast("Server error while approving request", "danger");
            }
        });
    }

}

$('#closeViewModalBtn').click(function () {
    $('#viewRequestModal').modal('hide'); // Close the modal
    loadUserRequests(); // Refresh the request list
});

/*==========START CASH IN ADVANCE==========*/
$(document).on('click', '.btn-view-cia', function () {
    const ciaId = $(this).data('id');
    if (!ciaId) return;

    // Load request data and fill the modal
    loadCIADetails(ciaId);
});


function loadCIADetails(ciaId) {
    $.get(`/TravelExpense/GetCIAViewDetails?id=${ciaId}`, function (data) {
        if (!data) {
            showToast("Failed to load request details", "danger");
            return;
        }

        // Fill modal with all info (including budget)
        fillCIAViewModal(data);

        // ✅ Show the modal
        $('#viewRequestModal').modal('show');
    }).fail(function () {
        showToast("Server error while loading request details", "danger");
    });
}


function fillCIAViewModal(data) {
    const role = sessionStorage.getItem("currentUserRole") || "";
    $('#vCIAID').text(data.ID);
    // 🔹 Travel Info
   /* $('#viewTarNo').text(data.TarNo || "");*/
    $('#vRequiredDate').text(formatJSONDate(data.RequiredDate));
    $('#vReturnDate').text(formatJSONDate(data.ReturnedDate));
    $('#vRequiredCash').text(data.RequiredCash ?? "");
    $('#vReason').text(data.Reason || "");

    if (data.RequiredCash > 5000000) {
        $('#containerBankInfo')
        const html = `
            <div class="card-header bg-primary text-white font-weight-bold">Bank Info</div>
            <div class="card-body row">
                        <div class="col-md-12"><strong>Account No:</strong> <span>${data.AccountNo}</span></div>
                        <div class="col-md-12"><strong>Beneficial Name:</strong> <span>${data.BeneficialName}</span></div>
                        <div class="col-md-12"><strong>Bank Branch:</strong> <span>${data.BankBranch}</span></div>
            </div>
        `
        $('#containerBankInfo').html(html);
    } else {
        $('#containerBankInfo').html('');
    }
    // SHow/Hide Container
    $("#containerTripInfo").hide();
    $("#containerCostInfo").hide();
    $("#containerEmpInfo").hide();
    $("#containerRequireInfo").show();
    $("#containerBankInfo").show();

    // 🔹 Requester Signature
    $('#viewRequesterSign').text(data.RequesterSign || "");
    $('#viewRequesterDate').text(formatJSONDate(data.CreatedDate));

    // 🔹 Approval Sections
    resetApprovalSections();

    const approvals = data.Approvals || [];
    showApprovalSections(role, approvals, data.StatusID);
}
/*==========END CASH IN ADVANCE============*/
$('#exportPDFBtn').click(function () {
    // 🔹 Clone the modal content
    const modal = document.querySelector('#viewRequestModal .modal-content');
    const cloned = modal.cloneNode(true);

    // 🔹 Remove footer
    const footer = cloned.querySelector('.modal-footer');
    if (footer) footer.remove();

    // 🔹 Get TAR No text from the live DOM
    const tarText = document.querySelector('#viewTarNo')?.textContent?.trim() || "TAR_No";

    // 🔹 Create a standalone header
    const tarOnly = document.createElement('h4');
    tarOnly.innerText = `TAR No: ${tarText}`;
    tarOnly.style.margin = "0 0 10px 0";
    tarOnly.style.fontWeight = "bold";

    // 🔹 Extract and prepare body content
    const body = cloned.querySelector('.modal-body');
    if (!body) {
        alert("No modal body found to export.");
        return;
    }

    // 🔹 Insert page break before FC Section (optional for multi-page layout)
    const fcSection = body.querySelector('#fcSection');
    if (fcSection) {
        const pageBreak = document.createElement('div');
        pageBreak.className = 'page-break';
        fcSection.before(pageBreak);
    }

    // 🔹 Combine into one wrapper
    const pdfElement = document.createElement('div');
    pdfElement.style.padding = '10px';
    pdfElement.appendChild(tarOnly);
    pdfElement.appendChild(body);

    // 🔹 Format filename with timestamp
    const now = new Date();
    const timestamp = now.toISOString().replace(/[:.-]/g, '_').slice(0, 16);
    const filename = `${tarText.replace(/\s+/g, '_')}_${timestamp}.pdf`;

    // 🔹 Export with html2pdf
    html2pdf()
        .set({
            margin: [0.5, 0.5, 0.5, 0.5],
            filename: filename,
            image: { type: 'jpeg', quality: 0.98 },
            html2canvas: { scale: 2, scrollY: 0 },
            jsPDF: { unit: 'in', format: 'a4', orientation: 'portrait', putOnlyUsedFonts: true },
            pagebreak: { mode: ['css', 'legacy'] }
        })
        .from(pdfElement)
        .toPdf()
        .get('pdf')
        .then(pdf => {
            const totalPages = pdf.internal.getNumberOfPages();
            for (let i = 1; i <= totalPages; i++) {
                pdf.setPage(i);
                pdf.setFontSize(10);
                pdf.text(`Page ${i} of ${totalPages}`, 0.5, 11);
            }
        })
        .save();
});

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

function formatNumber(num) {
    return num.toLocaleString('de-DE'); // e.g. 1.234.567
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

function convertDate(data) {
    const match = /\/Date\((\d+)\)\//.exec(data);
    if (match) {
        const timestamp = parseInt(match[1]);
        return new Date(timestamp).toLocaleDateString('en-GB');
    }
    return 'Invalid Date';
}