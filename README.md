# News Publisher 1.2.0 for diafaneia.eu

Ξεχωριστή εφαρμογή Windows για ασφαλή δημοσίευση άρθρων στη Χιώτικη Διαφάνεια.

Η έκδοση 1.2.0 διατηρεί ολόκληρη τη λειτουργική βάση 1.1.1 και αλλάζει μόνο το site-specific transport της Διαφάνειας: αντί για WordPress Application Password χρησιμοποιεί τον νέο, ανεξάρτητο **KARAGO Diafaneia Publisher Connector 1.0.0**.

## Λειτουργίες που διατηρούνται

- Smart Category Picker με πραγματικές WordPress κατηγορίες
- συχνές κατηγορίες, αναζήτηση και multi-select
- Draft/Publish με επιβεβαίωση
- προαιρετική featured image
- Yoast SEO title, meta description και focus keyphrase
- REQUEST_ID/idempotency και wrong-site protection
- ασφαλής αποθήκευση credential στο Windows Credential Manager
- διαχείριση απεριόριστων διαφημίσεων μέσα από το πρόγραμμα
- αλλαγή ονόματος και HTML κάθε διαφήμισης
- προσθήκη, διαγραφή και χειροκίνητη αλλαγή σειράς
- σταθερή ή τυχαία σειρά, με όλες τις διαφημίσεις ακριβώς μία φορά

## Νέα ασφαλής σύνδεση

- ιδιωτικό header `X-KARAGO-Key`
- κανένα WordPress username, password ή Application Password
- ένα ξεχωριστό, ανακλητό key ανά PC ή κινητό
- scope ανά συσκευή: «μόνο προσχέδια» ή «προσχέδια και δημοσίευση»
- έως 100 ταυτόχρονα ενεργές εξουσιοδοτημένες συσκευές
- ατομική προστασία REQUEST_ID κοινή για όλες τις συσκευές
- HTTPS-only, χωρίς redirects που θα μπορούσαν να μεταφέρουν το ιδιωτικό header σε άλλο host
- σαφές handling για 401/403/404/409/413/429/5xx και timeout

Το πλήρες API key αποθηκεύεται μόνο στο Windows Credential Manager του συγκεκριμένου PC. Ο Connector αποθηκεύει μόνο salted hash, hint και τα μη απόρρητα στοιχεία διαχείρισης της συσκευής.

## WordPress plugins

Νέο plugin προς εγκατάσταση:

`KARAGO_DIAFANEIA_PUBLISHER_CONNECTOR_1.0.0.zip`

Το υπάρχον `KARAGO Editorial Connector 0.4.0` δεν τροποποιείται. Το παλιό `News Publisher Bridge 1.0.0` επίσης παραμένει ανέγγιχτο μέσα στο πλήρες source για ασφαλή μετάβαση. Τα plugins μπορούν να συνυπάρχουν, επειδή έχουν διαφορετικά namespaces, keys και αποθηκευτικό χώρο.

## Εκτέλεση στα Windows

Κάντε πρώτα **Εξαγωγή όλων** από το portable ZIP και μετά ανοίξτε το `News-Publisher.exe` από τον αποσυμπιεσμένο φάκελο. Το `.exe` και τα συνοδευτικά native WPF `.dll` πρέπει να παραμένουν μαζί.

Στο πρόγραμμα πατήστε **Ρυθμίσεις σύνδεσης**, επικολλήστε το key που δημιουργήθηκε ειδικά για αυτό το PC και πατήστε **Έλεγχος σύνδεσης**. Η επιτυχής απάντηση εμφανίζει `CONNECTED`, όνομα συσκευής, scope και έκδοση Connector.

## Android

Ο νέος Connector έχει κοινό contract για Windows και Android και υποστηρίζει ανεξάρτητο key για κάθε κινητό. Η υπάρχουσα εφαρμογή KARAGO Mobile Publisher για Alithenia/Sportaki δεν τροποποιήθηκε από αυτή την αλλαγή. Μία μελλοντική Diafaneia Android έκδοση μπορεί να χρησιμοποιήσει τα ίδια `/status`, `/terms` και `/publish` endpoints χωρίς αλλαγή στο plugin.

## Διαφημίσεις και ασφαλές retry

Η πρώτη διαφήμιση της τελικής σειράς μπαίνει πριν από το άρθρο. Ακολουθεί ολόκληρο το άρθρο και όλες οι υπόλοιπες διαφημίσεις μπαίνουν μετά. Στην τυχαία λειτουργία η σειρά είναι τυχαία ανά REQUEST_ID αλλά παραμένει ίδια σε retry του ίδιου άρθρου, ώστε το idempotency payload να μην αλλάζει μετά από timeout.

Το πλήρες API contract βρίσκεται στο `DIAFANEIA-CONNECTOR-API-CONTRACT.md`.

