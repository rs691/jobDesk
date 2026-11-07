-- Run after setup, in jobdesk_synthetic. Fixed as-of: 2026-11-06.

-- Work orders by status

SELECT status, COUNT(*) AS orders FROM work_orders GROUP BY status ORDER BY status;

-- Overdue open work by crew

SELECT crew_id, COUNT(*) AS overdue_orders, ROUND(SUM(estimated_hours),2) AS estimated_hours FROM work_orders WHERE status IN ('scheduled','in_progress','blocked') AND scheduled_date < '2026-11-06' GROUP BY crew_id ORDER BY overdue_orders DESC, crew_id;

-- Outstanding invoice balance

SELECT ROUND(SUM(i.total_amount-COALESCE(p.paid,0)),2) AS outstanding FROM invoices i LEFT JOIN (SELECT invoice_id,SUM(amount) paid FROM payments GROUP BY invoice_id) p ON i.invoice_id=p.invoice_id;

-- Overdue invoice balance

SELECT COUNT(*) AS overdue_invoices,ROUND(SUM(i.total_amount-COALESCE(p.paid,0)),2) AS overdue_balance FROM invoices i LEFT JOIN (SELECT invoice_id,SUM(amount) paid FROM payments GROUP BY invoice_id) p ON i.invoice_id=p.invoice_id WHERE i.status='overdue';

-- Top five customers by billed revenue

SELECT c.customer_id,c.customer_name,ROUND(SUM(i.total_amount),2) AS billed FROM customers c JOIN work_orders w ON c.customer_id=w.customer_id JOIN invoices i ON w.work_order_id=i.work_order_id GROUP BY c.customer_id,c.customer_name ORDER BY billed DESC,c.customer_id LIMIT 5;

-- Materials below reorder point

SELECT material_id,stock_on_hand,reorder_point FROM materials WHERE stock_on_hand < reorder_point ORDER BY material_id;

-- Material reservation shortages

SELECT m.material_id,m.stock_on_hand,SUM(u.quantity) AS reserved_quantity,SUM(u.quantity)-m.stock_on_hand AS shortage FROM materials m JOIN material_usage u ON m.material_id=u.material_id WHERE u.usage_type='reserved' GROUP BY m.material_id,m.stock_on_hand HAVING SUM(u.quantity)>m.stock_on_hand ORDER BY shortage DESC,m.material_id LIMIT 10;

-- Blocked jobs explained

SELECT w.work_order_id,w.crew_id,n.note_text FROM work_orders w JOIN activity_notes n ON w.work_order_id=n.work_order_id WHERE w.status='blocked' AND n.note_type='status' ORDER BY w.work_order_id LIMIT 5;

-- Next seven days planned crew load

SELECT crew_id,COUNT(*) AS orders,ROUND(SUM(estimated_hours),2) AS hours FROM work_orders WHERE status IN ('scheduled','in_progress','blocked') AND scheduled_date BETWEEN '2026-11-06' AND '2026-11-12' GROUP BY crew_id ORDER BY hours DESC,crew_id;

-- Completed hours versus estimate by service

SELECT service_type,COUNT(*) AS orders,ROUND(SUM(estimated_hours),2) AS estimated,ROUND(SUM(actual_hours),2) AS actual FROM work_orders WHERE status='completed' GROUP BY service_type ORDER BY service_type;