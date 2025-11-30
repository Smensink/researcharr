import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Button from 'Components/Link/Button';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import Spinner from 'Components/Spinner';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import formatDateTime from 'Utilities/Date/formatDateTime';
import translate from 'Utilities/String/translate';
import './ImportSearches.css';

function ImportSearches() {
  const [jobs, setJobs] = useState([]);
  const [selectedJob, setSelectedJob] = useState(null);
  const [items, setItems] = useState([]);
  const [file, setFile] = useState(null);
  const [name, setName] = useState('');
  const [uploading, setUploading] = useState(false);
  const [loadingItems, setLoadingItems] = useState(false);

  const refreshJobs = useCallback(() => {
    createAjaxRequest({
      url: '/api/v1/importsearch',
      method: 'GET',
      dataType: 'json'
    }).request.then((data) => {
      setJobs(data);
      if (selectedJob) {
        const updated = data.find((j) => j.id === selectedJob.id);
        if (updated) {
          setSelectedJob(updated);
        }
      }
    });
  }, [selectedJob]);

  const refreshItems = useCallback((jobId) => {
    if (!jobId) {
      return;
    }

    setLoadingItems(true);

    createAjaxRequest({
      url: `/api/v1/importsearch/${jobId}/items`,
      method: 'GET',
      dataType: 'json'
    }).request.then((data) => {
      setItems(data);
    }).always(() => setLoadingItems(false));
  }, []);

  useEffect(() => {
    refreshJobs();
  }, [refreshJobs]);

  useEffect(() => {
    if (selectedJob) {
      refreshItems(selectedJob.id);
    }
  }, [selectedJob, refreshItems]);

  useEffect(() => {
    const hasActive = jobs.some((j) => j.status === 'Processing' || j.status === 'Pending');
    if (!hasActive) {
      return undefined;
    }

    const timer = setInterval(refreshJobs, 5000);
    return () => clearInterval(timer);
  }, [jobs, refreshJobs]);

  const onUpload = (event) => {
    event.preventDefault();

    if (!file) {
      return;
    }

    setUploading(true);

    const formData = new window.FormData();
    formData.append('file', file);
    if (name) {
      formData.append('name', name);
    }

    createAjaxRequest({
      url: '/api/v1/importsearch',
      method: 'POST',
      data: formData,
      processData: false,
      contentType: false,
      dataType: 'json'
    }).request.then((job) => {
      setFile(null);
      setName('');
      refreshJobs();
      setSelectedJob(job);
    }).always(() => setUploading(false));
  };

  const selectedJobItems = useMemo(() => items, [items]);

  return (
    <PageContent title="Search Imports">
      <PageContentBody>
        <div className="importSearches__layout">
          <div className="importSearches__jobs">
            <form className="importSearches__upload" onSubmit={onUpload}>
              <strong>Upload PubMed/Ovid/Embase export (RIS/XML)</strong>
              <input
                type="text"
                placeholder="Name (optional)"
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
              <input
                type="file"
                accept=".ris,.txt,.xml"
                onChange={(e) => setFile(e.target.files[0])}
              />
              <Button type="submit" disabled={!file || uploading}>
                {uploading ? 'Uploading…' : 'Import'}
              </Button>
            </form>

            {jobs.map((job) => (
              <div
                key={job.id}
                className={`importSearches__jobRow${selectedJob?.id === job.id ? ' isActive' : ''}`}
                onClick={() => setSelectedJob(job)}
              >
                <div><strong>{job.name}</strong></div>
                <div>{job.source}</div>
                <div>{translate(job.status)}</div>
                <div>{`${job.matched}/${job.total} matched • ${job.failed} failed`}</div>
                <div>{job.created ? formatDateTime(job.created) : ''}</div>
              </div>
            ))}
          </div>

          <div className="importSearches__items">
            {selectedJob ? (
              <>
                <div style={{ marginBottom: 8 }}>
                  <strong>{selectedJob.name}</strong>
                  <div>Status: {translate(selectedJob.status)}</div>
                  <div>Source: {selectedJob.source}</div>
                  <div>Matched: {selectedJob.matched} / {selectedJob.total}</div>
                </div>
                {loadingItems && <Spinner />}
                {!loadingItems && (
                  <table className="importSearches__itemsTable">
                    <thead>
                      <tr>
                        <th>{translate('Title')}</th>
                        <th>{translate('Authors')}</th>
                        <th>DOI</th>
                        <th>PMID</th>
                        <th>{translate('Status')}</th>
                        <th>Message</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedJobItems.map((item) => (
                        <tr key={item.id}>
                          <td>{item.title}</td>
                          <td>{item.authors}</td>
                          <td>{item.doi}</td>
                          <td>{item.pmid}</td>
                          <td>{translate(item.status)}</td>
                          <td>{item.message}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </>
            ) : (
              <div>Select a job to view items</div>
            )}
          </div>
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default ImportSearches;
