=== KARAGO Diafaneia Publisher Connector ===
Contributors: karago
Tags: editorial, rest-api, publishing, diafaneia
Requires at least: 6.0
Requires PHP: 7.4
Stable tag: 1.0.0
License: Proprietary

Ιδιωτικός Connector αποκλειστικά για το https://diafaneia.eu.

== Τι κάνει ==

* Συνδέει Windows και Android εφαρμογές χωρίς WordPress Application Password.
* Δημιουργεί ξεχωριστό API key για κάθε PC ή κινητό.
* Επιτρέπει ανάκληση μίας συσκευής χωρίς να επηρεάζονται οι υπόλοιπες.
* Υποστηρίζει δικαίωμα «μόνο προσχέδια» ή «προσχέδια και δημοσίευση».
* Επιστρέφει πραγματικές κατηγορίες και tags, μαζί με parent/count.
* Υποστηρίζει Draft/Publish, featured image, inline images και Yoast SEO.
* Προστατεύει από διπλές δημοσιεύσεις με ατομικό REQUEST_ID/idempotency.
* Απορρίπτει λάθος site, μη HTTPS ρύθμιση, άγνωστες κατηγορίες και αλλοιωμένα images.

== Εγκατάσταση ==

1. WordPress → Πρόσθετα → Προσθήκη νέου → Μεταφόρτωση προσθέτου.
2. Ανεβάστε το ZIP του Connector και ενεργοποιήστε το.
3. Ρυθμίσεις → KARAGO Diafaneia.
4. Δημιουργήστε ένα κλειδί για κάθε συσκευή, με αναγνωρίσιμο όνομα.
5. Αντιγράψτε κάθε κλειδί αμέσως στην αντίστοιχη εφαρμογή. Εμφανίζεται μόνο μία φορά.

Το υπάρχον KARAGO Editorial Connector 0.4.0 και το News Publisher Bridge μπορούν να παραμείνουν εγκατεστημένα. Ο νέος Connector έχει διαφορετικό plugin, διαφορετικό REST namespace, διαφορετικά tables και διαφορετικά keys.

== REST API ==

Authentication header:

X-KARAGO-Key: [device-specific key]

Endpoints:

GET /wp-json/karago-diafaneia/v1/status
GET /wp-json/karago-diafaneia/v1/terms
POST /wp-json/karago-diafaneia/v1/publish

Δεν χρησιμοποιούνται username, WordPress password ή Application Password.

== Security ==

Το πλήρες API key δεν αποθηκεύεται. Αποθηκεύεται μόνο salted WordPress password hash, αναγνωριστικό/hint και στοιχεία διαχείρισης της συσκευής. Το πλήρες κλειδί επιστρέφεται μόνο στη σελίδα δημιουργίας του και δεν γράφεται σε log ή exception.

Η δημοσίευση είναι κλειδωμένη στο diafaneia.eu, το API λειτουργεί μόνο με HTTPS ρύθμιση, η ανακατεύθυνση δεν απαιτείται, και κάθε REQUEST_ID δεσμεύεται ατομικά στη βάση πριν από οποιαδήποτε δημιουργία άρθρου.

== Changelog ==

= 1.0.0 =
* Πρώτη ανεξάρτητη έκδοση για τη Διαφάνεια.
* Multi-device keys, ανάκληση και scopes.
* status/terms/publish contract.
* Atomic REQUEST_ID idempotency.
* Draft/Publish, εικόνες και Yoast SEO.

