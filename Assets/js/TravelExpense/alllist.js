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

                // Show loading indicator (optional)
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
                                    childHtml += `
                                <a href="/CashInAdvance/Index?t=${btoa(item.ID)}" 
                                   class="btn btn-sm btn-outline-success ml-1">
                                    <i class="fa fa-edit"></i> Edit
                                </a>`;
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