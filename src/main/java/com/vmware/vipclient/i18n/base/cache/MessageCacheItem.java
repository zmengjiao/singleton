/*
 * Copyright 2019 VMware, Inc.
 * SPDX-License-Identifier: EPL-2.0
 */
package com.vmware.vipclient.i18n.base.cache;

import java.util.HashMap;
import java.util.Map;

import com.vmware.vipclient.i18n.VIPCfg;

public class MessageCacheItem implements CacheItem {
	public MessageCacheItem() {
		
	}
	
	public MessageCacheItem (Map<String, String> dataMap, String etag, long timestamp, Long maxAgeMillis) {
		super();
		this.addCachedData(dataMap);
		this.etag = etag;
		this.timestamp = timestamp;
		this.maxAgeMillis = maxAgeMillis;
	}
	
	public MessageCacheItem (Map<String, String> dataMap) {
		super();
		if (dataMap != null)
			this.addCachedData(dataMap);
	}
	
	
	private String etag;
	private long timestamp;
	private Long maxAgeMillis = 864000000l;
	
	public final Map<String, String> cachedData = new HashMap<String, String>();
	
	public synchronized void addCachedData(Map<String, String> cachedData) {
		if (cachedData != null) 
			this.cachedData.putAll(cachedData);
	}
	
	public synchronized void addCacheItem (MessageCacheItem cacheItem) {
		this.addCachedData(cacheItem.getCachedData());
		this.etag = cacheItem.etag;
		this.timestamp = cacheItem.timestamp;
		this.maxAgeMillis = cacheItem.maxAgeMillis;
	}
		
	public synchronized String getEtag() {
		return etag;
	}

	public synchronized void setEtag(String etag) {
		this.etag = etag;
	}

	public synchronized long getTimestamp() {
		return timestamp;
	}

	public synchronized void setTimestamp(long timestamp) {
		this.timestamp = timestamp;
	}
    
    public Map<String, String> getCachedData() {
		return cachedData;
	}

	public synchronized Long getMaxAgeMillis() {
		return maxAgeMillis;
	}

	public synchronized void setMaxAgeMillis(Long maxAgeMillis) {
		this.maxAgeMillis = maxAgeMillis;
	}

	public boolean isExpired() {
    	// If maxAgeFromConfig is present, it means it is using the old way 
    	// of caching expiration, so do not expire individual CacheItem object
    	if (VIPCfg.getInstance().getCacheExpiredTime() != 0) {
    		return false;
    	}
    	
    	Long responseTimeStamp = this.getTimestamp();
    	if (responseTimeStamp == null) {
    		return true;
    	}
    	
    	Long maxAgeResponse = this.getMaxAgeMillis();
    	if (maxAgeResponse != null) {
    		maxAgeMillis = maxAgeResponse;
    	}
    		  	
    	return System.currentTimeMillis() - responseTimeStamp > maxAgeMillis;
    }
	
}